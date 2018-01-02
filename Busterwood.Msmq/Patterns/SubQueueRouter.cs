using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// Moves selected messages into sub-queues.
    /// This is a safe way to filter messages with explicit transaction control. The alternative, directly using a cursor, 
    /// is not safe as the cursor position does not update when a receive transaction is rolled-back.
    /// </summary>
    public class SubQueueRouter : IProcessor
    {
        readonly string _inputFormatName;
        readonly Func<Message, SubQueue> _router;
        QueueReader _input;
        SubQueue _posionSubQueue;
        QueueTransaction _transaction;
        Task _running;

        /// <summary>Name of the subqueue to move messages to when they cannot be routed, or the router function returns null.</summary>
        public string UnroutableSubQueue { get; set; } = "Poison";

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public Properties PeekProperties { get; } = Properties.AppSpecific | Properties.Label | Properties.Extension | Properties.LookupId;

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to <see cref="UnroutableSubQueue"/> of the input queue</summary>
        public Action<long, QueueTransaction> BadMessageHandler { get; set; }

        public SubQueueRouter(string inputFormatName, Func<Message, SubQueue> router) 
        {
            Contract.Requires(inputFormatName != null);
            Contract.Requires(router != null);
            _inputFormatName = inputFormatName;
            _router = router;
            BadMessageHandler = MoveToUnroutableSubQueue;
        }

        public Task<Task> StartAsync()
        {
            _input = new QueueReader(_inputFormatName);
            if (Queues.IsTransactional(_input.FormatName) == QueueTransactional.Transactional)
                _transaction = QueueTransaction.Single;
            _running = RunAsync();
            return Task.FromResult(_running);
        }

        async Task RunAsync()
        {
            await Task.Yield();
            try
            {
                for(;;)
                {
                    Message peeked = _input.Peek(PeekProperties, TimeSpan.Zero) ?? await _input.PeekAsync(PeekProperties);
                    try
                    {
                        var subQueue = GetRoute(peeked);
                        Queues.MoveMessage(_input, subQueue, peeked.LookupId, _transaction);
                    }
                    catch (RouteException ex)
                    {
                        if (peeked.LookupId != 0)
                            MoveToUnroutableSubQueue(peeked.LookupId, _transaction);
                    }
                }
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // queue handle was closed, i.e. stopped
            }
            catch (ObjectDisposedException)
            {
                // queue handle was closed, i.e. stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected SubQueue GetRoute(Message msg)
        {
            SubQueue subQueue = null;
            try
            {
                subQueue = _router(msg);
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
            _input?.Dispose(); // this will stop any pending peek operations
            _posionSubQueue?.Dispose();
            return _running;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
        }

        private void MoveToUnroutableSubQueue(long lookupId, QueueTransaction transaction)
        {
            try
            {
                if (_posionSubQueue == null)
                    _posionSubQueue = new SubQueue(_input.FormatName + ";" + UnroutableSubQueue);

                Queues.MoveMessage(_input, _posionSubQueue, lookupId, transaction);
                return;
            }
            catch (QueueException e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={UnroutableSubQueue}}} {{error={e.Message}}}");
            }
        }

    }
}
