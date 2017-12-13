using System;
using BusterWood.Msmq;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;
using System.Linq;
using System.Collections.Generic;

namespace MsmqPatterns
{
    /// <summary>
    /// Use this class to send a message and get acknowledgement that is has been sent.
    /// The <see cref="SendAsync(Message, QueueTransaction, Queue)"/> method will throw <see cref="AcknowledgmentException"/> on errors, and throw a <see cref="TimeoutException"/> if the message fails to reach the destination queue in the time allowed.
    /// You must call <see cref="StartAsync"/> before calling <see cref="SendAsync(Message, QueueTransaction, Queue)"/>.
    /// </summary>
    /// <remarks>
    /// You can use one instance per process (singleton), share the instance between multiple queues, or even one instance per output queue.
    /// </remarks>
    public class QueueSender : IProcessor
    {
        readonly Cache<PostedMessageHandle, TaskCompletionSource<MessageClass>> _reachQueue = new Cache<PostedMessageHandle, TaskCompletionSource<MessageClass>>(null, TimeSpan.FromMinutes(5));
        QueueReader _adminQueue;
        Task _run;

        public string AdminQueueFormatName { get; }

        public Properties AdminFilter { get; } = Properties.CorrelationId  | Properties.Class | Properties.ResponseQueue;

        /// <summary>The time allowed for a message to reach a destination queue before a <see cref="TimeoutException"/> is thrown by <see cref="SendAsync(Message, Queue)"/></summary>
        public TimeSpan ReachQueueTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Creates a new sender that waits for confirmation of deliver</summary>
        /// <param name="adminQueue">The format name of the <see cref="Message.AdministrationQueue"/></param>
        public QueueSender(string adminQueue)
        {
            Contract.Requires(adminQueue != null);
            AdminQueueFormatName = adminQueue;
        }

        /// <summary>Starts listening for acknowledgement messages</summary>
        public Task<Task> StartAsync()
        {
            _adminQueue = new QueueReader(AdminQueueFormatName);
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            try
            {
                for (;;)
                {
                    var msg = await _adminQueue.ReadAsync(AdminFilter);
                    var tcs = ReachQueueCompletionSource(new PostedMessageHandle(msg.ResponseQueue, msg.CorrelationId));
                    var ack = msg.Acknowledgement();
                    switch (ack)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        case MessageClass.ReachQueue:
                            Task.Run(() => tcs.TrySetResult(ack)); // set result is synchronous by default, make it async
                            break;
                        case MessageClass.ReachQueueTimeout:
                        case MessageClass.AccessDenied:
                        case MessageClass.BadDestinationQueue:
                        case MessageClass.BadEncryption:
                        case MessageClass.BadSignature:
                        case MessageClass.CouldNotEncrypt:
                        case MessageClass.HopCountExceeded:
                        case MessageClass.NotTransactionalMessage:
                        case MessageClass.NotTransactionalQueue:
                        case MessageClass.Deleted:
                        case MessageClass.QueueDeleted:
                        case MessageClass.QueueExceedQuota:
                            Task.Run(() => tcs.TrySetException(new AcknowledgmentException(msg.ResponseQueue, ack))); // set result is synchronous by default, make it async
                            break;
                        case MessageClass.QueuePurged:
                        case MessageClass.ReceiveTimeout:
                        case MessageClass.Received:
                            break; // not handled here, can we detect if these were requested?
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // stopped
            }
            catch (ObjectDisposedException)
            {
                // stopped
            }
        }

        /// <summary>Stop listening for acknowledgement messages</summary>
        public Task StopAsync()
        {
            _adminQueue?.Dispose();
            return _run;
        }

        /// <summary>Stops the processor</summary>
        public void Dispose()
        {
            try
            {
                StopAsync().Wait();
            }
            catch
            {
                // dispose must not throw exceptions
            }
        }

        /// <summary>
        /// Posts a <paramref name="message"/> to the <paramref name="queue"/> with acknowledgement requested to be sent to <see cref="AdminQueueFormatName"/>. 
        /// </summary>
        public PostedMessageHandle Post(Message message, QueueTransaction transaction, QueueWriter queue)
        {
            Contract.Requires(message != null);
            Contract.Requires(queue != null);
            Contract.Assert(_run != null);

            message.AcknowledgmentTypes |= AcknowledgmentTypes.ReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue.FormatName;
            queue.Write(message, transaction);
            return new PostedMessageHandle(queue.FormatName, message.Id, message.LookupId);
        }

        /// <summary>
        /// Sends a <paramref name="message"/> to the <paramref name="queue"/> and waits for it to be delivered. 
        /// Waits for responses from all queues when the <paramref name="queue"/> is a multi-element format name.
        /// Note that the transaction MUST commit before the acknowledgements are received.
        /// </summary>
        /// <returns>Task that completes when the message has been delivered</returns>
        /// <exception cref="TimeoutException">Thrown if the message does not reach the queue before the <see cref="ReachQueueTimeout"/> has been reached</exception>
        /// <exception cref="AcknowledgmentException">Thrown if something bad happens, e.g. message could not be sent, access denied, the queue was purged, etc</exception>
        public Task SendAsync(Message message, QueueTransaction transaction, QueueWriter queue)
        {
            Contract.Requires(message != null);
            Contract.Requires(queue != null);
            Contract.Requires(transaction == null || transaction == QueueTransaction.None || transaction == QueueTransaction.Single);
            Contract.Assert(_run != null);

            Post(message, transaction, queue);
            return WaitForDelivery(new PostedMessageHandle(message.Id, queue.FormatName));
        }

        /// <summary>
        /// Waits for positive or negative delivery of a message to a <paramref name="destinationFormatName"/>
        /// Waits for responses from all queues when the <paramref name="destinationFormatName"/> is a multi-element format name.
        /// </summary>
        /// <param name="messageId">The message that was sent</param>
        /// <param name="destinationFormatName">the format name of the queue the message was sent to</param>
        public Task WaitForDelivery(PostedMessageHandle fnId)
        {
            Contract.Requires(!fnId.IsEmpty);

            // handle multiple destination format names (comma separated list)
            if (fnId.FormatName.IndexOf(',') >= 0)
            {
                return Task.WhenAll(fnId.FormatName.Split(',')
                    .Select(formatName => new PostedMessageHandle(formatName, fnId.MessageId))
                    .Select(ReachQueueCompletionSource)
                    .Select(qtcs => qtcs.Task)
                );
            }

            // single element format name
            var key = new PostedMessageHandle(fnId.FormatName, fnId.MessageId);
            var tcs = ReachQueueCompletionSource(key);
            return tcs.Task;
        }

        internal Task WaitForDelivery(IReadOnlyCollection<PostedMessageHandle> sent)
        {
            Contract.Requires(sent != null);
            Contract.Requires(sent.Count > 0);
            return Task.WhenAll(sent.Select(WaitForDelivery));
        }

        TaskCompletionSource<MessageClass> ReachQueueCompletionSource(PostedMessageHandle key) => _reachQueue.GetOrAdd(key, _ => new TaskCompletionSource<MessageClass>());
        
    }

    public struct PostedMessageHandle : IEquatable<PostedMessageHandle>
    {
        public string FormatName { get; }
        public string MessageId { get; }
        public long LookupId { get; }

        public bool IsEmpty => FormatName == null;

        public PostedMessageHandle(string formatName, string messageId) : this(formatName, messageId, 0)
        {
        }

        public PostedMessageHandle(string formatName, string messageId, long lookupId)
        {
            Contract.Requires(messageId != null);
            Contract.Requires(formatName != null);
            FormatName = formatName;
            MessageId = messageId;
            LookupId = lookupId;
        }

        public bool Equals(PostedMessageHandle other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(FormatName, other.FormatName)
                && StringComparer.OrdinalIgnoreCase.Equals(MessageId, other.MessageId);
        }

        public override bool Equals(object obj) => obj is PostedMessageHandle && Equals((PostedMessageHandle)obj);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(FormatName) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(MessageId);
    }

}
