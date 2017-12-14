using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace BusterWood.MsmqPatterns
{
    /// <summary>
    /// Moves selected messages into sub-queues.
    /// This is a safe way to filter messages with explicit transaction control. The alternative, directly using a cursor, 
    /// is not safe as the cursor position does not update when a receive transaction is rolled-back.
    /// </summary>
    public class SubQueueFilterRouter : IProcessor
    {
        readonly string _inputFormatName;
        readonly Func<Message, SubQueueMover> _router;
        QueueReader _input;
        SubQueueMover _posionSubQueue;
        QueueTransaction _transaction;
        Task _run;

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public Properties PeekFilter { get; } = Properties.AppSpecific | Properties.Label | Properties.Extension | Properties.LookupId;

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<long, QueueTransaction> BadMessageHandler { get; set; }

        public SubQueueFilterRouter(string inputFormatName, Func<Message, SubQueueMover> router) 
        {
            Contract.Requires(inputFormatName != null);
            Contract.Requires(router != null);
            _inputFormatName = inputFormatName;
            _router = router;
            BadMessageHandler = MoveToPoisonSubqueue;
        }

        public Task<Task> StartAsync()
        {
            _input = new QueueReader(_inputFormatName);
            if (Queue.IsTransactional(_input.FormatName) == QueueTransactional.Transactional)
                _transaction = QueueTransaction.Single;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            try
            {
                for(;;)
                {
                    Message peeked = await _input.PeekAsync(PeekFilter);
                    try
                    {
                        var subQueue = GetRoute(peeked);
                        subQueue.MoveFrom(_input, peeked.LookupId, _transaction);
                    }
                    catch (RouteException ex)
                    {
                        if (peeked.LookupId != 0)
                            MoveToPoisonSubqueue(peeked.LookupId, _transaction);
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

        protected SubQueueMover GetRoute(Message msg)
        {
            SubQueueMover subQueue = null;
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
            return _run;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
        }

        private void MoveToPoisonSubqueue(long lookupId, QueueTransaction transaction)
        {
            try
            {
                if (_posionSubQueue == null)
                    _posionSubQueue = new SubQueueMover(_input.FormatName + ";Poison");

                _posionSubQueue.MoveFrom(_input, lookupId, transaction);
                return;
            }
            catch (QueueException e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={_posionSubQueue?.SubQueueName()}}} {{error={e.Message}}}");
            }
        }

    }
}
