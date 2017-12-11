using System;
using BusterWood.Msmq;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;
using System.Linq;

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
    public class Sender : IProcessor
    {
        readonly Cache<FormatNameAndMsgId, TaskCompletionSource<MessageClass>> _reachQueue = new Cache<FormatNameAndMsgId, TaskCompletionSource<MessageClass>>(null, TimeSpan.FromMinutes(5));
        readonly string _adminQueueFormatName;
        Queue _adminQueue;
        Task _run;

        public Properties AdminFilter { get; } = Properties.CorrelationId  | Properties.Class | Properties.ResponseQueue;

        /// <summary>The time allowed for a message to reach a destination queue before a <see cref="TimeoutException"/> is thrown by <see cref="SendAsync(Message, Queue)"/></summary>
        public TimeSpan ReachQueueTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>Creates a new sender that waits for confirmation of deliver</summary>
        /// <param name="adminQueue">The format name of the <see cref="Message.AdministrationQueue"/></param>
        public Sender(string adminQueue)
        {
            Contract.Requires(adminQueue != null);
            _adminQueueFormatName = adminQueue;
        }

        /// <summary>Starts listening for acknowledgement messages</summary>
        public Task<Task> StartAsync()
        {
            _adminQueue = Queue.Open(_adminQueueFormatName, QueueAccessMode.Receive);
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            try
            {
                for (;;)
                {
                    var msg = await _adminQueue.ReceiveAsync(AdminFilter);
                    var tcs = ReachQueueCompletionSource(new FormatNameAndMsgId(msg.ResponseQueue, msg.CorrelationId));
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

        /// <summary>Sends a <paramref name="message"/> to the <paramref name="queue"/> and waits for it to be delivered</summary>
        /// <returns>Task that completes when the message has been delivered</returns>
        /// <exception cref="TimeoutException">Thrown if the message does not reach the queue before the <see cref="ReachQueueTimeout"/> has been reached</exception>
        /// <exception cref="AcknowledgmentException">Thrown if something bad happens, e.g. message could not be sent, access denied, the queue was purged, etc</exception>
        public async Task SendAsync(Message message, QueueTransaction transaction, Queue queue)
        {
            Contract.Requires(message != null);
            Contract.Requires(queue != null);
            Contract.Assert(_run != null);

            message.AcknowledgmentTypes |= AcknowledgmentTypes.ReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue.FormatName;
            queue.Post(message, transaction);

            var formatName = queue.FormatName;
            var tcs = ReachQueueCompletionSource(new FormatNameAndMsgId(formatName, message.Id));
            await tcs.Task;
        }

        /// <summary>Send a message to many queues at the same time and wait for all acknowledgements</summary>
        public async Task SendToManyAsync(Message message, QueueTransaction transaction, params Queue[] queues)
        {
            Contract.Requires(message != null);
            Contract.Requires(queues != null);
            Contract.Requires(queues.Length > 0);
            Contract.Assert(_run != null);

            message.AcknowledgmentTypes |= AcknowledgmentTypes.ReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue.FormatName;
            var multiElementFormatName = string.Join(",", queues.Select(q => q.FormatName)); 

            var tasks = new Task[queues.Length];
            using (var q = Queue.Open(multiElementFormatName, QueueAccessMode.Send))
            {
                q.Post(message, transaction);
            }

            // expand host name
            var hostNames = queues.Select(q => q.FormatName).ToArray();

            await Task.WhenAll(hostNames
                .Select(host => new FormatNameAndMsgId(host, message.Id)) 
                .Select(ReachQueueCompletionSource)
                .Select(tcs => tcs.Task)
            );
        }

        //NOTE: no overload that takes a QueueTransaction as we would not get the acknowledgement until after the transaction has committed.

        TaskCompletionSource<MessageClass> ReachQueueCompletionSource(FormatNameAndMsgId key)
        {
            return _reachQueue.GetOrAdd(key, _ => new TaskCompletionSource<MessageClass>());
        }

        struct FormatNameAndMsgId : IEquatable<FormatNameAndMsgId>
        {
            public string FormatName { get;  }
            public string MessageId { get;  }

            public FormatNameAndMsgId(string formatName, string messageId)
            {
                FormatName = formatName;
                MessageId = messageId;
            }

            public bool Equals(FormatNameAndMsgId other)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(FormatName, other.FormatName)
                    && StringComparer.OrdinalIgnoreCase.Equals(MessageId, other.MessageId);
            }

            public override bool Equals(object obj) => obj is FormatNameAndMsgId && Equals((FormatNameAndMsgId)obj);

            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(FormatName) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(MessageId);
        }
        
    }
}
