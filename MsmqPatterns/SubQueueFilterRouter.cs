using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
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
        readonly Queue _input;
        readonly Func<Message, Queue> _getSubQueue;
        readonly Transaction _transaction;
        volatile bool _stop;
        Task _run;

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public Properties PeekFilter { get; } = Properties.AppSpecific | Properties.Label | Properties.Extension | Properties.LookupId;

        /// <summary>Timeout used so <see cref="StopAsync"/> can stop this processor</summary>
        public TimeSpan StopTime { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<long, Transaction> BadMessageHandler { get; set; }

        public SubQueueFilterRouter(string inputQueue, Func<Message, Queue> getSubQueueName) 
            : this(Queue.Open(inputQueue, QueueAccessMode.Receive), getSubQueueName)
        {
            Contract.Requires(inputQueue != null);
            Contract.Requires(getSubQueueName != null);
        }

        public SubQueueFilterRouter(Queue input, Func<Message, Queue> getSubQueueName)
        {
            Contract.Requires(input != null);
            Contract.Requires(getSubQueueName != null);
            _input = input;
            _getSubQueue = getSubQueueName;
            BadMessageHandler = MoveToPoisonSubqueue;
            if (Queue.IsTransactional(input.FormatName) == QueueTransactional.Transactional)
                _transaction = Transaction.Single;
        }

        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            try
            {
                while (!_stop)
                {
                    Message peeked = await _input.PeekAsync(PeekFilter, StopTime);
                    if (peeked == null)
                        continue;

                    try
                    {
                        var subQueue = GetRoute(peeked);
                        _input.Move(peeked.LookupId, subQueue, _transaction);
                    }
                    catch (RouteException ex)
                    {
                        if (peeked.LookupId != 0)
                            MoveToPoisonSubqueue(peeked.LookupId, _transaction);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected Queue GetRoute(Message msg)
        {
            Queue subQueue = null;
            try
            {
                subQueue = _getSubQueue(msg);
                if (subQueue == null)
                    throw new NullReferenceException("route");
                return subQueue;
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
            _posionSubQueue?.Dispose();
        }

        Queue _posionSubQueue;
        private void MoveToPoisonSubqueue(long lookupId, Transaction transaction)
        {
            try
            {
                if (_posionSubQueue == null)
                    _posionSubQueue = Queue.Open(_input.FormatName + ";Poison", QueueAccessMode.Move);

                _input.Move(lookupId, _posionSubQueue, transaction);
                return;
            }
            catch (Win32Exception e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={_posionSubQueue?.SubQueue()}}} {{error={e.Message}}}");
            }
        }

    }
}
