using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// Routes messages in a transaction. Up to <see cref="MaxBatchSize"/> messages are included in each transaction 
    /// because each transaction has a relatively large overhead.
    /// </summary>
    public class TransactionalRouter : Router
    {
        protected SubQueue _batchQueue;

        public TransactionalRouter(string inputQueueFormatName, Postman sender, Func<Message, QueueWriter> route)
            : base(inputQueueFormatName, sender, route)
        {
            Contract.Requires(sender != null);
            Contract.Requires(route != null);
            Contract.Requires(inputQueueFormatName != null);
        }

        /// <summary>
        /// Maximum number of messages batched into one transaction.
        /// We try to include multiple messages in a batch as it is MUCH faster (up to 10x)
        /// </summary>
        public int MaxBatchSize { get; set; } = 250;

        public string InProgressSubQueue { get; set; } = "batch";

        public override Task StopAsync()
        {
            _batchQueue?.Dispose();
            return base.StopAsync();
        }

        /*
        * 0) Recover - wait for acks for each message in batch
        * 1) Peek message
        * 2) begin transaction
        * 3)   move to "batch" subqueue
        * 4)   send
        * 5) commit
        * 6) for each message in batch
        * 7)   wait for ack 
        *         on success remove message from "batch" subqueue (Transaction.Single)
        *         on failure move to InProgressSubQueue
        */

        protected override async Task RunAsync()
        {
            await Task.Yield();
            _batchQueue = new SubQueue(InputQueueFormatName + ";" + InProgressSubQueue);
            try
            {
                await Recover(); // clean up the existing batch (if any)

                var sent = new List<Tracking>();
                for (;;)
                {
                    if (_input.Peek(PeekProperties, TimeSpan.Zero) == null)
                        await _input.PeekAsync(PeekProperties); // wait for next message
                    RouteBatch(sent);
                    await WaitForAcknowledgements(sent);
                    sent.Clear();
                }
            }
            catch (ObjectDisposedException)
            {
                Console.Error.WriteLine("INFO: stopping");
                // Stop was called
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                Console.Error.WriteLine("INFO: stopping");
                // Stop was called
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("WARNING: " + ex);
            }
        }

        private async Task Recover()
        {
            var sent = new List<Tracking>();
            for (; ; )
            {
                var msg = _batchQueue.Peek(PeekProperties, TimeSpan.Zero);
                if (msg == null)
                    break;
            }
            if (sent.Count == 0)
                return;

            Console.Error.WriteLine($"INFO recovering, waiting for {sent.Count} acknowledgements");
            await WaitForAcknowledgements(sent);
            Console.Error.WriteLine($"INFO recovered");
        }

        private void RouteBatch(List<Tracking> sent)
        {
            try
            {
                using (var txn = new QueueTransaction())
                {
                    RouteBatchCore(sent, txn);
                    txn.Commit();
                }
            }
            catch (RouteException ex)
            {
                Console.Error.WriteLine($"WARN {ex.Message} {{Destination={ex.Destination}}}");
                BadMessageHandler(_input, ex.LookupId, QueueTransaction.Single);
                sent.Clear();
            }
        }

        protected void RouteBatchCore(List<Tracking> sent, QueueTransaction txn)
        {
            long lookupId = 0;
            LookupAction action = LookupAction.PeekFirst;
            for (int i = 0; i < MaxBatchSize; i++)
            {
                // peek for the next message
                var msg = _input.Lookup(Properties.All, lookupId, action, TimeSpan.Zero);
                if (msg == null)
                    break;
                action = LookupAction.PeekNext;
                lookupId = msg.LookupId;

                // move to the batch subqueue so we know what we sent
                Queues.MoveMessage(_input, _batchQueue, msg.LookupId, txn);

                // route to message to the destination
                var dest = GetRoute(msg);
                sent.Add(Sender.RequestDelivery(msg, dest, txn));
            }
        }

        private async Task WaitForAcknowledgements(List<Tracking> sent)
        {
            if (sent.Count == 0)
                return;

            Console.Error.WriteLine($"DEBUG Waiting for {sent.Count} acknowledgements");

            foreach (var item in sent)
            {
                try
                {
                    await Sender.WaitForDeliveryAsync(item);
                    _batchQueue.Lookup(Properties.LookupId, item.LookupId, timeout: TimeSpan.Zero, transaction: QueueTransaction.Single);
                }
                catch (AcknowledgmentException ex)
                {
                    Console.Error.WriteLine("WARNING " + ex);
                    using (var txn = new QueueTransaction())
                    {
                        _batchQueue.MarkRejected(item.LookupId); // send a acknowledgement that the message has been rejected
                        Queues.MoveMessage(_batchQueue, _posionQueue, item.LookupId, txn);
                        txn.Commit();
                    }
                }
                catch (AggregateException ex)
                {
                    //TODO: handle sent error to multi-element format name
                    Console.Error.WriteLine($"WARNING multi-elements format names are not yet supported");
                    throw;
                }
            }
        }
    }
}
