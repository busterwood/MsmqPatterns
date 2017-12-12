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
        public MsmqTransactionalRouter(string inputQueueFormatName, Sender sender, Func<Message, Queue> route)
            : base (inputQueueFormatName, sender, route)
        {
            Contract.Requires(sender != null);
            Contract.Requires(route != null);
            Contract.Requires(inputQueueFormatName != null);
        }

        protected override async Task RunAsync()
        {
            await base.RunAsync();
            try
            {
                await SendBatchFromSubQueue(); // clean up the existing batch (if any)

                for (;;)
                {
                    if (MoveBatchtoSubQueue() > 0)
                        await SendBatchFromSubQueue();      // send messages
                    else
                        await _input.PeekAsync(PeekFilter); // wait for next message
                }
            }
            catch (ObjectDisposedException)
            {
                // Stop was called
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // Stop was called
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("WARNING: " + ex);
                throw;
            }
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

        int MoveBatchtoSubQueue()
        {
            using (var txn = new QueueTransaction())
            {
                int moved = MoveMessageToInProgess(txn);
                if (moved > 0)
                    txn.Commit();
                return moved;
            }
        }

        int MoveMessageToInProgess(QueueTransaction txn)
        {
            int moved;
            for (moved = 0; moved < MaxBatchSize; moved++)
            {
                var peeked = _input.Peek(Properties.LookupId, TimeSpan.Zero);
                if (peeked == null)
                    break;
                _input.Move(peeked.LookupId, _inProgressMove, txn);
            }
            return moved;
        }

        async Task SendBatchFromSubQueue()
        {
            for (;;)
            {
                using (var txn = new QueueTransaction())
                {
                    try
                    {
                        var sent = RouteBatchOfMessages(txn);
                        if (sent.Count == 0)
                        {
                            txn.Abort();
                            return;
                        }

                        txn.Commit();  // must commit before waiting for delivery
                        await Sender.WaitForDelivery(sent);
                        return;
                    }
                    catch (RouteException ex)
                    {
                        txn.Abort();
                        //TODO: log what happened and why
                        Console.Error.WriteLine($"WARN {ex.Message}S {{Destination={ex.Destination}}}");
                        BadMessageHandler(_inProgressRead, ex.LookupId, QueueTransaction.Single);
                    }
                }
            }
        }

        List<FormatNameAndMsgId> RouteBatchOfMessages(QueueTransaction txn)
        {
            var sent = new List<FormatNameAndMsgId>();
            for (int i = 0; i < MaxBatchSize; i++)
            {
                var msg = RouteMessage(txn);
                if (msg.IsEmpty)
                    break;
                sent.Add(msg);
            }
            return sent;
        }

        FormatNameAndMsgId RouteMessage(QueueTransaction txn)
        {
            var msg = _inProgressRead.Receive(Properties.All, timeout: TimeSpan.Zero, transaction: txn);
            if (msg == null)
                return default(FormatNameAndMsgId);

            var dest = GetRoute(msg);
            try
            {
                Sender.Post(msg, txn, dest);
                return new FormatNameAndMsgId(dest.FormatName, msg.Id);
            }
            catch (QueueException ex)
            {
                // we cannot send to that queue
                throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest?.FormatName);
            }
        }

    }
}