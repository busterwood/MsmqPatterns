using System;
using System.Messaging;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;

namespace MsmqPatterns
{
    /// <summary>
    /// Use this class to send a message and get acknowledgement that is has been sent.
    /// The <see cref="SendAsync(Message, MessageQueue)"/> method will throw <see cref="AcknowledgmentException"/> on errors, and throw a <see cref="TimeoutException"/> if the message fails to reach the destination queue in the time allowed.
    /// Yo must call <see cref="StartAsync"/> before calling <see cref="SendAsync(Message, MessageQueue, MessageQueueTransactionType)"/>.
    /// </summary>
    public class Sender : IProcessor
    {
        readonly Cache<string, TaskCompletionSource<Acknowledgment>> _reachQueue;
        readonly MessageQueue _adminQueue;
        volatile bool _stop;
        Task _run;

        public MessagePropertyFilter Filter { get; } = new MessagePropertyFilter
        {
            AppSpecific = true,
            Label = true,
            Extension = true,
            LookupId = true,
            CorrelationId = true,
        };

        /// <summary>Timeout used when peeking for messages</summary>
        /// <remarks>the higher this value the slower the <see cref="StopAsync"/> method will be</remarks>
        public TimeSpan StopTime { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>The time allowed for a message to reach a destination queue before a <see cref="TimeoutException"/> is thrown by <see cref="SendAsync(Message, MessageQueue)"/></summary>
        public TimeSpan ReachQueueTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public Sender(MessageQueue adminQueue)
        {
            Contract.Requires(adminQueue != null);
            _adminQueue = adminQueue;
            _reachQueue = new Cache<string, TaskCompletionSource<Acknowledgment>>(null, TimeSpan.FromMinutes(5));
        }

        /// <summary>Starts listening for acknowledgement messages</summary>
        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            _adminQueue.MessageReadPropertyFilter = Filter;
            while (!_stop)
            {
                using (var msg = await _adminQueue.TryRecieveAsync(StopTime))
                {
                    if (msg == null)
                        continue;

                    var tcs = ReachQueueCompletionSource(msg.Id);
                    switch (msg.Acknowledgment)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        case Acknowledgment.ReachQueue:
                            Task.Run(() => tcs.TrySetResult(msg.Acknowledgment)); // set result is synchronous by default, make it async
                            break;
                        case Acknowledgment.ReachQueueTimeout:
                            Task.Run(() => tcs.TrySetException(new TimeoutException())); // set result is synchronous by default, make it async
                            break;
                        case Acknowledgment.AccessDenied:
                        case Acknowledgment.BadDestinationQueue:
                        case Acknowledgment.BadEncryption:
                        case Acknowledgment.BadSignature:
                        case Acknowledgment.CouldNotEncrypt:
                        case Acknowledgment.HopCountExceeded:
                        case Acknowledgment.NotTransactionalMessage:
                        case Acknowledgment.NotTransactionalQueue:
                        case Acknowledgment.Purged:
                        case Acknowledgment.QueueDeleted:
                        case Acknowledgment.QueueExceedMaximumSize:
                            Task.Run(() => tcs.TrySetException(new AcknowledgmentException(msg.Acknowledgment))); // set result is synchronous by default, make it async
                            break;
                        case Acknowledgment.QueuePurged:
                        case Acknowledgment.ReceiveTimeout:
                        case Acknowledgment.Receive:
                            break; // not handled here, can we detect if these were requested?
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            }
        }
        
        /// <summary>Stop listening for acknowledgement messages</summary>
        public Task StopAsync()
        {
            _stop = true;
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
            _adminQueue.Dispose();
        }

        /// <summary>Send a <paramref name="message"/> to the <paramref name="dest"/> queue and wait for it to be delivered</summary>
        /// <returns>Task that completes when the message has been delivered</returns>
        /// <exception cref="TimeoutException">Thrown if the message does not reach the queue before the <see cref="ReachQueueTimeout"/> has been reached</exception>
        /// <exception cref="AcknowledgmentException">Thrown if something bad happens, e.g. message could not be sent, access denied, the queue was purged, etc</exception>
        public Task SendAsync(Message message, MessageQueue dest, MessageQueueTransactionType transactionType)
        {
            Contract.Requires(message != null);
            Contract.Requires(dest != null);
            Contract.Assert(_run != null);

            message.AcknowledgeType |= AcknowledgeTypes.FullReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue;
            dest.Send(message, transactionType);
            var tcs = ReachQueueCompletionSource(message.Id);
            return tcs.Task;
        }

        TaskCompletionSource<Acknowledgment> ReachQueueCompletionSource(string msgId) => _reachQueue.GetOrAdd(msgId, id => new TaskCompletionSource<Acknowledgment>());

    }
}
