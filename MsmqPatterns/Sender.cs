using System;
using System.Messaging;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;
using System.Linq;
using System.Net;

namespace MsmqPatterns
{
    /// <summary>
    /// Use this class to send a message and get acknowledgement that is has been sent.
    /// The <see cref="SendAsync(Message, MessageQueue)"/> method will throw <see cref="AcknowledgmentException"/> on errors, and throw a <see cref="TimeoutException"/> if the message fails to reach the destination queue in the time allowed.
    /// You must call <see cref="StartAsync"/> before calling <see cref="SendAsync(Message, MessageQueueTransactionType, MessageQueue)"/>.
    /// </summary>
    /// <remarks>
    /// You can use one instance per process (singleton), share the instance between multiple queues, or even one instance per output queue.
    /// </remarks>
    public class Sender : IProcessor
    {
        static readonly ReadThroughCache<string, string> _hostNameCache = new ReadThroughCache<string, string>(new DnsDataSource(), 100, TimeSpan.FromMinutes(10));
        readonly Cache<FormatNameAndMsgId, TaskCompletionSource<Acknowledgment>> _reachQueue = new Cache<FormatNameAndMsgId, TaskCompletionSource<Acknowledgment>>(null, TimeSpan.FromMinutes(5));
        readonly MessageQueue _adminQueue;
        volatile bool _stop;
        Task _run;

        public MessagePropertyFilter Filter { get; } = new MessagePropertyFilter
        {
            CorrelationId = true,
            Acknowledgment = true,
            ResponseQueue = true,
        };

        /// <summary>Timeout used when peeking for messages</summary>
        /// <remarks>the higher this value the slower the <see cref="StopAsync"/> method will be</remarks>
        public TimeSpan StopTime { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>The time allowed for a message to reach a destination queue before a <see cref="TimeoutException"/> is thrown by <see cref="SendAsync(Message, MessageQueue)"/></summary>
        public TimeSpan ReachQueueTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Opens (or creates) the administration queue using format name.  Note: you need to pass a queue path to create the queue
        /// </summary>
        /// <param name="adminQueueName"></param>
        public Sender(string adminQueueName)
        {
            Contract.Requires(!string.IsNullOrEmpty(adminQueueName));

            if (MessageQueue.Exists(adminQueueName))
                _adminQueue = new MessageQueue(adminQueueName, QueueAccessMode.Receive);
            else
                _adminQueue = MessageQueue.Create(adminQueueName);

            if (_adminQueue.Transactional)
                throw new InvalidOperationException("Admin queue CANNOT be transactional");
        }

        public Sender(MessageQueue adminQueue)
        {
            Contract.Requires(adminQueue != null);
            Contract.Requires(adminQueue.Transactional == false);
            _adminQueue = adminQueue;
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

                    var tcs = ReachQueueCompletionSource(new FormatNameAndMsgId(msg.ResponseQueue.FormatName, msg.CorrelationId));
                    switch (msg.Acknowledgment)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        case Acknowledgment.ReachQueue:
                            Task.Run(() => tcs.TrySetResult(msg.Acknowledgment)); // set result is synchronous by default, make it async
                            break;
                        case Acknowledgment.ReachQueueTimeout:
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
                            Task.Run(() => tcs.TrySetException(new AcknowledgmentException(msg.ResponseQueue.FormatName, msg.Acknowledgment))); // set result is synchronous by default, make it async
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

        /// <summary>Sends a <paramref name="message"/> to the <paramref name="queue"/> and waits for it to be delivered</summary>
        /// <returns>Task that completes when the message has been delivered</returns>
        /// <exception cref="TimeoutException">Thrown if the message does not reach the queue before the <see cref="ReachQueueTimeout"/> has been reached</exception>
        /// <exception cref="AcknowledgmentException">Thrown if something bad happens, e.g. message could not be sent, access denied, the queue was purged, etc</exception>
        public async Task SendAsync(Message message, MessageQueueTransactionType transactionType, MessageQueue queue)
        {
            Contract.Requires(message != null);
            Contract.Requires(queue != null);
            Contract.Assert(_run != null);

            message.AcknowledgeType |= AcknowledgeTypes.FullReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue;
            queue.Send(message, transactionType);

            var formatName = await ExpandHostName(queue.FormatName);
            var tcs = ReachQueueCompletionSource(new FormatNameAndMsgId(formatName, message.Id));
            await tcs.Task;
        }

        // example: Direct=OS:notknownserver\\private$\\some-queue
        async Task<string> ExpandHostName(string formatName)
        {
            var bits = formatName.Split(':', '\\');
            var host = bits[1];
            if (host.IndexOf('.') >= 0)
                return formatName;
            try
            {
                var fullHost = await _hostNameCache.GetAsync(bits[1]);
                return formatName.Replace(bits[1], fullHost); // TODO: only replace first
            }
            catch
            {
                return formatName;
            }
        }

        /// <summary>Send a message to many queues at the same time and wait for all acknowledgements</summary>
        public async Task SendToManyAsync(Message message, MessageQueueTransactionType transactionType, params MessageQueue[] queues)
        {
            Contract.Requires(message != null);
            Contract.Requires(queues != null);
            Contract.Requires(queues.Length > 0);
            Contract.Assert(_run != null);

            message.AcknowledgeType |= AcknowledgeTypes.FullReachQueue;
            message.TimeToReachQueue = ReachQueueTimeout;
            message.AdministrationQueue = _adminQueue;
            var multiElementFormatName = string.Join(",", queues.Select(q => q.FormatName)); 

            var tasks = new Task[queues.Length];
            using (var q = new MessageQueue(multiElementFormatName, QueueAccessMode.Send))
            {
                q.Send(message, transactionType);
            }

            // expand host name
            var hostNamesTask = queues.Select(q => ExpandHostName(q.FormatName)).ToArray();
            await Task.WhenAll(hostNamesTask);
            var hostNames = hostNamesTask.Select(t => t.Result).ToArray();

            await Task.WhenAll(hostNames
                .Select(host => new FormatNameAndMsgId(host, message.Id)) 
                .Select(ReachQueueCompletionSource)
                .Select(tcs => tcs.Task)
            );
        }

        //NOTE: no overload that takes a MessageQueueTransaction as we would not get the acknowledgement until after the transaction has committed.

        TaskCompletionSource<Acknowledgment> ReachQueueCompletionSource(FormatNameAndMsgId key)
        {
            return _reachQueue.GetOrAdd(key, _ => new TaskCompletionSource<Acknowledgment>());
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

        class DnsDataSource : IDataSource<string, string>
        {
            public string this[string key] => Dns.GetHostEntry(key)?.HostName ?? key;

            public async Task<string> GetAsync(string key)
            {
                var entry = await Dns.GetHostEntryAsync(key);
                return entry?.HostName ?? key;
            }
        }
    }
}
