using System;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MsmqPatterns
{
    /// <summary>Routes batches of messages between local <see cref = "Queue"/> using a MSMQ transaction</summary>
    public class MsmqTransactionalRouter : TransactionalRouter
    {
        public MsmqTransactionalRouter(string inputQueueFormatName, QueueSender sender, Func<Message, QueueWriter> route)
            : base (inputQueueFormatName, sender, route)
        {
            Contract.Requires(sender != null);
            Contract.Requires(route != null);
            Contract.Requires(inputQueueFormatName != null);
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
         *         on failure ?  move to dead letter?
         */

        protected override async Task RunAsync()
        {
            await base.RunAsync();
            try
            {
                await Recover(); // clean up the existing batch (if any)

                var sent = new List<PostedMessageHandle>();
                for (;;)
                {
                    await _input.PeekAsync(PeekFilter); // wait for next message
                    PostBatch(sent);
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
                throw;
            }
        }

        private async Task Recover()
        {
            var sent = new List<PostedMessageHandle>();
            for(;;)
            {
                var msg = _inProgressRead.Peek(PeekFilter, TimeSpan.Zero);
                if (msg == null)
                    break;
            }
            if (sent.Count == 0)
                return;

            Console.Error.WriteLine($"INFO recovering, waiting for {sent.Count} acknowledgements");
            await WaitForAcknowledgements(sent);
            Console.Error.WriteLine($"INFO recovered");
        }

        private void PostBatch(List<PostedMessageHandle> sent)
        {
            try
            {
                using (var txn = new QueueTransaction())
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
                        _inProgressMove.MoveFrom(_input, msg.LookupId, txn);

                        // route to message to the destination
                        var dest = GetRoute(msg);
                        sent.Add(Sender.Post(msg, txn, dest));
                    }

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

        private async Task WaitForAcknowledgements(List<PostedMessageHandle> sent)
        {
            if (sent.Count == 0)
                return;

            Console.Error.WriteLine($"DEBUG Waiting for {sent.Count} acknowledgements");

            foreach (var item in sent)
            {
                try
                {
                    await Sender.WaitForDelivery(item);
                    _inProgressRead.Lookup(Properties.LookupId, item.LookupId, timeout: TimeSpan.Zero, transaction: QueueTransaction.Single);
                }
                catch (AcknowledgmentException ex)
                {
                    Console.Error.WriteLine("WARN:" + ex);
                    _posionQueue.MoveFrom(_inProgressRead, item.LookupId, QueueTransaction.Single);
                }
                catch (AggregateException ex)
                {
                    //TODO: handle sent error to multi-element format name
                    Console.Error.WriteLine($"WARN multi-elements format names are not yet supported");
                    throw;
                }
            }
        }


    }
}