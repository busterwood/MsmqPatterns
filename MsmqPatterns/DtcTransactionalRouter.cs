using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Transactions;
using BusterWood.Msmq;

namespace MsmqPatterns
{
    /// <summary>Routes batches of messages in local or remote queues using DTC <see cref = "TransactionScope"/></summary>
    public class DtcTransactionalRouter : TransactionalRouter
    {
        public DtcTransactionalRouter(string inputQueueFormatName, Postman sender, Func<Message, QueueWriter> route)
            : base(inputQueueFormatName, sender, route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(route != null);
        }

        protected override async Task RunAsync()
        {
            await base.RunAsync();
            try
            {
                for (;;)
                {
                    var msg = await _input.PeekAsync(PeekFilter);
                    await OnNewMessage(msg);
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

        async Task OnNewMessage(Message peeked)
        {
            try
            {
                using (var txn = new TransactionScope(TransactionScopeOption.RequiresNew))
                {
                    int count = await RouteBatchOfMessages();
                    if (count > 0)
                        txn.Complete();
                }
            }
            catch (RouteException ex)
            {
                //TODO: log what happened and why
                Console.Error.WriteLine($"WARN {ex.Message} {{Destination={ex.Destination}}}");
                BadMessageHandler(_input, ex.LookupId, QueueTransaction.Dtc); //TODO: what type is good here?
            }
        }

        async Task<int> RouteBatchOfMessages()
        {
            //TODO: use Sender
            int sent = 0;
            for (int i = 0; i < MaxBatchSize; i++)
            {
                if (RouteMessage())
                    sent++;
                else
                    break;
            }

            return sent;
        }

        private bool RouteMessage()
        {
            Message msg = _input.Read(Properties.All, timeout: TimeSpan.Zero, transaction: QueueTransaction.Dtc); //note: no waiting
            if (msg == null)
                return false;
            var dest = GetRoute(msg);
            try
            {
                dest.Write(msg, QueueTransaction.Dtc);
                return true;
            }
            catch (QueueException ex)
            {
                // we cannot send to that queue
                throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest?.FormatName);
            }
        }
    }
}