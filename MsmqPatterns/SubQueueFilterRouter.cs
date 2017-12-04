using System;
using System.ComponentModel;
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
        public TimeSpan StopTime { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<long, bool?> BadMessageHandler { get; set; }

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
            BadMessageHandler = MoveToPoisonSubqueue;
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
            try
            {
                while (!_stop)
                {
                    using (Message peeked = await _input.TryPeekAsync(StopTime))
                    {
                        if (peeked == null)
                            continue;

                        try
                        {
                            var subQueueName = GetRoute(peeked);
                            _input.MoveMessage(subQueueName, peeked.LookupId);
                        }
                        catch (RouteException ex)
                        {
                            MoveToPoisonSubqueue(peeked.LookupId);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected string GetRoute(Message msg)
        {
            string subQueueName = null;
            try
            {
                subQueueName = _getSubQueueName(msg);
                if (subQueueName == null)
                    throw new NullReferenceException("route");
                return subQueueName;
            }
            catch (Exception ex)
            {
                throw new RouteException("Failed to get route", ex, msg.LookupId);
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

        private void MoveToPoisonSubqueue(long lookupId, bool? transactional = null)
        {
            const string poisonSubqueue = "Poison";
            try
            {
                _input.MoveMessage(poisonSubqueue, lookupId, transactional);
                return;
            }
            catch (Win32Exception e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={poisonSubqueue}}} {{error={e.Message}}}");
            }
        }

    }
}
