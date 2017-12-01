using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>
    /// Moves selected messages into sub-queues.
    /// This is a safe way to filter messages with explicit transaction control. The alternative, directly using a cursor, 
    /// is not safe as the cursor position does not update when a receive transaction is rolled-back.
    /// </summary>
    public class SubQueueFilterRouter : IProcessor
    {
        readonly MessageQueue _input;
        readonly Func<Message, string> _getSubQueueName;
        volatile bool _stop;
        Task _run;

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public MessagePropertyFilter PeekFilter { get; } = new MessagePropertyFilter
        {
            AppSpecific = true,
            Label = true,
            Extension = true,
            LookupId = true,
        };

        /// <summary>Timeout used so <see cref="StopAsync"/> can stop this processor</summary>
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

        public SubQueueFilterRouter(string inputQueue, Func<Message, string> getSubQueueName) 
            : this(new MessageQueue(inputQueue, QueueAccessMode.Receive), getSubQueueName)
        {
            Contract.Requires(inputQueue != null);
            Contract.Requires(getSubQueueName != null);
        }

        public SubQueueFilterRouter(MessageQueue input, Func<Message, string> getSubQueueName)
        {
            Contract.Requires(input != null);
            Contract.Requires(getSubQueueName != null);
            _input = input;
            _getSubQueueName = getSubQueueName;
        }

        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            _input.MessageReadPropertyFilter = PeekFilter;

            var action = PeekAction.Current;
            using (var cur = _input.CreateCursor())
            {
                while (!_stop)
                {
                    using (Message peeked = await _input.TryPeekAsync(ReceiveTimeout, cur, action))
                    {
                        if (peeked == null)
                            continue;
                        var sqn = _getSubQueueName(peeked);
                        if (sqn != null)
                            _input.MoveMessage(sqn, peeked.LookupId);
                        action = PeekAction.Next;
                    }
                }
            }
        }

        public Task StopAsync()
        {
            _stop = true;
            return _run;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
            _input.Dispose();
        }
    }
}
