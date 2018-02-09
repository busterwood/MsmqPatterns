using System;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;
using System.Linq;
using System.Collections.Generic;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// Use this class to send a message and get acknowledgement that is has been sent.
    /// The <see cref="SendAsync(Message, QueueTransaction, Queue)"/> method will throw <see cref="AcknowledgmentException"/> on errors, and throw a <see cref="TimeoutException"/> if the message fails to reach the destination queue in the time allowed.
    /// You must call <see cref="StartAsync"/> before calling <see cref="SendAsync(Message, QueueTransaction, Queue)"/>.
    /// </summary>
    /// <remarks>
    /// You can use one instance per process (singleton), share the instance between multiple queues, or even one instance per output queue.
    /// </remarks>
    public class Postman : IProcessor
    {
        readonly Cache<Tracking, TaskCompletionSource<MessageClass>> _reachQueue = new Cache<Tracking, TaskCompletionSource<MessageClass>>(null, TimeSpan.FromMinutes(10));
        readonly Cache<Tracking, TaskCompletionSource<MessageClass>> _receiveQueue = new Cache<Tracking, TaskCompletionSource<MessageClass>>(null, TimeSpan.FromMinutes(10));
        QueueReader _adminQueue;
        Task _run;

        /// <summary>The format name of the administration queue used for acknowledgement messages</summary>
        public string AdminQueueFormatName { get; }

        public Properties AdminProperties { get; } = Properties.CorrelationId  | Properties.Class | Properties.ResponseQueue;

        /// <summary>The time allowed for a message to reach a destination queue before a <see cref="TimeoutException"/> is thrown by <see cref="DeliverAsync(Message, QueueWriter, QueueTransaction)"/></summary>
        public TimeSpan ReachQueueTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Creates a new sender that waits for confirmation of deliver</summary>
        /// <param name="adminQueueFormatName">The format name of the <see cref="Message.AdministrationQueue"/></param>
        public Postman(string adminQueueFormatName)
        {
            Contract.Requires(adminQueueFormatName != null);
            AdminQueueFormatName = adminQueueFormatName;
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
            await Task.Yield();
            try
            {
                for (;;)
                {
                    var msg = _adminQueue.Read(AdminProperties, TimeSpan.Zero) ?? await _adminQueue.ReadAsync(AdminProperties);
                    var ack = msg.Acknowledgement();
                    switch (ack)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        case MessageClass.ReachQueue:
                            var tcs = ReachQueueCompletionSource(new Tracking(msg.ResponseQueue, msg.CorrelationId));
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
                        case MessageClass.QueuePurged:
                        case MessageClass.QueueExceedQuota:
                            var tcs1 = ReachQueueCompletionSource(new Tracking(msg.ResponseQueue, msg.CorrelationId));
                            Task.Run(() => tcs1.TrySetException(new AcknowledgmentException(msg.ResponseQueue, ack))); // set result is synchronous by default, make it async
                            break;
                        case MessageClass.Received:
                            var tcs2 = ReceiveCompletionSource(new Tracking(msg.ResponseQueue, msg.CorrelationId));
                            Task.Run(() => tcs2.TrySetResult(ack)); // set result is synchronous by default, make it async
                            break; 
                        case MessageClass.ReceiveTimeout:
                            var tcs3 = ReceiveCompletionSource(new Tracking(msg.ResponseQueue, msg.CorrelationId));
                            Task.Run(() => tcs3.TrySetException(new AcknowledgmentException(msg.ResponseQueue, ack))); // set result is synchronous by default, make it async
                            break; 
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
        public Tracking RequestDelivery(Message message, QueueWriter queue, QueueTransaction transaction = null)
        {
            Contract.Requires(message != null);
            Contract.Requires(queue != null);
            Contract.Assert(_run != null);
            Contract.Assert(_adminQueue != null);

            message.AcknowledgmentTypes |= AcknowledgmentTypes.FullReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue.FormatName;
            queue.Write(message, transaction);

            // acknowledgements for multicast messages get an empty DestinationQueue, so we need to remove it here
            var formatName = queue.FormatName.StartsWith("multicast=", StringComparison.OrdinalIgnoreCase)? "" : queue.FormatName;
            return new Tracking(formatName, message.Id, message.LookupId);
        }

        /// <summary>
        /// Waits for positive or negative delivery of a message.
        /// Waits for responses from all queues when the <see cref="Tracking.FormatName"/> is a multi-element format name.
        /// </summary>
        public void WaitForDelivery(Tracking posted)
        {
            try
            {
                WaitForDeliveryAsync(posted).Wait();
            }
            catch (AggregateException e)  when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// Waits for positive or negative delivery of a message.
        /// Waits for responses from all queues when the <see cref="Tracking.FormatName"/> is a multi-element format name.
        /// </summary>
        public Task WaitForDeliveryAsync(Tracking tracking)
        {
            Contract.Requires(!tracking.IsEmpty);

            // handle multiple destination format names (comma separated list)
            if (tracking.FormatName.IndexOf(',') >= 0)
            {
                return Task.WhenAll(tracking.FormatName.Split(',')
                    .Select(formatName => new Tracking(formatName, tracking.MessageId))
                    .Select(ReachQueueCompletionSource)
                    .Select(qtcs => qtcs.Task)
                );
            }

            // single element format name
            var tcs = ReachQueueCompletionSource(tracking);
            return tcs.Task;
        }

        /// <summary>
        /// Waits for positive or negative receive of a message.
        /// Waits for responses from all queues when the <see cref="Tracking.FormatName"/> is a multi-element format name.
        /// </summary>
        public void WaitToBeReceived(Tracking posted)
        {
            try
            {
                WaitToBeReceivedAsync(posted).Wait();
            }
            catch (AggregateException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// Waits for positive or negative receive of a message.
        /// Waits for responses from all queues when the <see cref="Tracking.FormatName"/> is a multi-element format name.
        /// </summary>
        public Task WaitToBeReceivedAsync(Tracking tracking)
        {
            Contract.Requires(!tracking.IsEmpty);

            // handle multiple destination format names (comma separated list)
            if (tracking.FormatName.IndexOf(',') >= 0)
            {
                return Task.WhenAll(tracking.FormatName.Split(',')
                    .Select(formatName => new Tracking(formatName, tracking.MessageId))
                    .Select(ReceiveCompletionSource)
                    .Select(qtcs => qtcs.Task)
                );
            }

            // single element format name
            var tcs = ReceiveCompletionSource(tracking);
            return tcs.Task;
        }

        internal Task WaitForDelivery(IReadOnlyCollection<Tracking> sent)
        {
            Contract.Requires(sent != null);
            Contract.Requires(sent.Count > 0);
            return Task.WhenAll(sent.Select(WaitForDeliveryAsync));
        }

        TaskCompletionSource<MessageClass> ReachQueueCompletionSource(Tracking key) => _reachQueue.GetOrAdd(key, _ => new TaskCompletionSource<MessageClass>());

        TaskCompletionSource<MessageClass> ReceiveCompletionSource(Tracking key) => _receiveQueue.GetOrAdd(key, _ => new TaskCompletionSource<MessageClass>());
        
    }

    /// <summary>Delivery tracking information for the <see cref="Postman"/></summary>
    public struct Tracking : IEquatable<Tracking>
    {
        /// <summary>The queue the message was sent to</summary>
        public string FormatName { get; }

        /// <summary>The Id of the message that was sent</summary>
        public MessageId MessageId { get; }

        /// <summary>The queue-specific lookup Id of the message</summary>
        public long LookupId { get; }

        /// <summary>If this the default tracking information?</summary>
        public bool IsEmpty => FormatName == null;

        public Tracking(string formatName, MessageId messageId) : this(formatName, messageId, 0)
        {
        }

        public Tracking(string formatName, MessageId messageId, long lookupId)
        {
            Contract.Requires(formatName != null); // might be empty if sent to a multicast address
            Contract.Requires(!messageId.IsNullOrEmpty());
            FormatName = formatName;
            MessageId = messageId;
            LookupId = lookupId;
        }

        public bool Equals(Tracking other) => StringComparer.OrdinalIgnoreCase.Equals(FormatName, other.FormatName) && MessageId.Equals(other.MessageId);

        public override bool Equals(object obj) => obj is Tracking && Equals((Tracking)obj);

        public override int GetHashCode() => IsEmpty ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(FormatName) ^ MessageId.GetHashCode();
    }


    public static class Extensions
    {
        public static Tracking RequestDelivery(this QueueWriter queue, Message message, Postman postman, QueueTransaction txn = null) => postman.RequestDelivery(message, queue, txn);

        public static void WaitForDelivery(this Tracking tracking, Postman postman) => postman.WaitForDelivery(tracking);

        public static Task WaitForDeliveryAsync(this Tracking tracking, Postman postman) => postman.WaitForDeliveryAsync(tracking);

        public static void WaitToBeReceived(this Tracking tracking, Postman postman) => postman.WaitToBeReceived(tracking);

        public static Task WaitToBeReceivedAsync(this Tracking tracking, Postman postman) => postman.WaitToBeReceivedAsync(tracking);

        /// <summary>
        /// Sends a <paramref name="message"/> to the <paramref name="queue"/> and waits for it to be delivered. 
        /// Waits for responses from all queues when the <paramref name="queue"/> is a multi-element format name.
        /// Note that the transaction MUST commit before the acknowledgements are received.
        /// </summary>
        /// <exception cref="TimeoutException">Thrown if the message does not reach the queue before the <see cref="ReachQueueTimeout"/> has been reached</exception>
        /// <exception cref="AcknowledgmentException">Thrown if something bad happens, e.g. message could not be sent, access denied, the queue was purged, etc</exception>
        public static void Deliver(this QueueWriter queue, Message message, Postman postman, QueueTransaction transaction = null)
        {
            Contract.Requires(queue != null);
            Contract.Requires(message != null);
            Contract.Requires(postman != null);
            Contract.Requires(transaction == null || transaction == QueueTransaction.None || transaction == QueueTransaction.Single);

            var t = postman.RequestDelivery(message, queue, transaction);
            postman.WaitForDelivery(t);
        }

        /// <summary>
        /// Sends a <paramref name="message"/> to the <paramref name="queue"/> and waits for it to be delivered. 
        /// Waits for responses from all queues when the <paramref name="queue"/> is a multi-element format name.
        /// Note that the transaction MUST commit before the acknowledgements are received.
        /// </summary>
        /// <returns>Task that completes when the message has been delivered</returns>
        /// <exception cref="TimeoutException">Thrown if the message does not reach the queue before the <see cref="ReachQueueTimeout"/> has been reached</exception>
        /// <exception cref="AcknowledgmentException">Thrown if something bad happens, e.g. message could not be sent, access denied, the queue was purged, etc</exception>
        public static Task DeliverAsync(this QueueWriter queue, Message message, Postman postman, QueueTransaction transaction = null)
        {
            Contract.Requires(queue != null);
            Contract.Requires(message != null);
            Contract.Requires(postman != null);
            Contract.Requires(transaction == null || transaction == QueueTransaction.None || transaction == QueueTransaction.Single);

            var t = postman.RequestDelivery(message, queue, transaction);
            return postman.WaitForDeliveryAsync(t);
        }

    }
}
