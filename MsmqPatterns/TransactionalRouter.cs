using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Transactions;
using BusterWood.Msmq;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages in a transaction. Up to <see cref="MaxBatchSize"/> messages are included in each transaction 
    /// because each transaction has a relatively large overhead.
    /// </summary>
    public abstract class TransactionalRouter : Router
    {
        protected Queue _inProgressRead;
        protected Queue _inProgressMove;

        protected TransactionalRouter(string inputQueueFormatName, Func<Message, Queue> route)
            : base(inputQueueFormatName, route)
        {
            Contract.Requires(route != null);
            Contract.Requires(inputQueueFormatName != null);
        }

        /// <summary>
        /// Maximum number of messages batched into one transaction.
        /// We try to include multiple messages in a batch as it is MUCH faster (up to 10x)
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        public string InProgressSubQueue { get; set; } = "batch";

        protected override Task RunAsync()
        {
            _inProgressRead = Queue.Open(_inputQueueFormatName + ";" + InProgressSubQueue, QueueAccessMode.Receive);
            _inProgressMove = Queue.Open(_inputQueueFormatName + ";" + InProgressSubQueue, QueueAccessMode.Move);
            return base.RunAsync();
        }

        public override Task StopAsync()
        {
            _inProgressRead?.Dispose();
            _inProgressMove?.Dispose();
            return base.StopAsync();
        }
    }

    /// <summary>Routes batches of messages between local <see cref="Queue"/> using a MSMQ transaction</summary>
    public class MsmqTransactionalRouter : TransactionalRouter
    {        
        public MsmqTransactionalRouter(string inputQueueFormatName, Func<Message, Queue> route)
            : base(inputQueueFormatName, route)
        {
            Contract.Requires(route != null);
            Contract.Requires(inputQueueFormatName != null);
        }

        protected override void OnNewMessage(Message peeked)
        {
            using (var txn = new QueueTransaction())
            {
                try
                {
                    int count = RouteBatchOfMessages(txn);
                    if (count > 0)
                        txn.Commit();

                    // now we wait for acknowledgement of the messages we just sent
                }
                catch (RouteException ex)
                {
                    txn.Abort();
                    //TODO: log what happened and why
                    Console.Error.WriteLine($"WARN {ex.Message}S {{Destination={ex.Destination}}}");
                    BadMessageHandler(ex.LookupId, QueueTransaction.Single);
                }
            }
        }

        int RouteBatchOfMessages(QueueTransaction txn)
        {
            int toSend;
            for (toSend = 0; toSend < MaxBatchSize; toSend++)
            {
                var peeked = _input.Peek(Properties.Label, TimeSpan.Zero);
                if (peeked == null)
                    break;
                _input.Move(peeked.LookupId, _inProgressRead, txn);
            }

            int sent = 0;
            for (int i = 0; i < MaxBatchSize; i++)
            {
                if (RouteMessage(txn))
                    sent++;
                else
                    break;
            }
            return sent;
        }

        private bool RouteMessage(QueueTransaction txn)
        {
            var msg = _inProgressRead.Receive(Properties.All, timeout: TimeSpan.Zero, transaction: txn);
            {
                if (msg != null)
                    return false;

                var dest = GetRoute(msg);

                try
                {
                    dest.Post(msg, txn);
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

    /// <summary>Routes batches of messages in local or remote queues using DTC <see cref="TransactionScope"/></summary>
    public class DtcTransactionalRouter : TransactionalRouter
    {

        public DtcTransactionalRouter(string inputQueueFormatName, Func<Message, Queue> route)
            : base(inputQueueFormatName, route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(route != null);
        }

        protected override void OnNewMessage(Message peeked)
        {
            try
            {
                using (var txn = new TransactionScope(TransactionScopeOption.RequiresNew))
                {
                    int count = RouteBatchOfMessages();
                    if (count > 0)
                        txn.Complete();
                }
            }
            catch (RouteException ex)
            {
                //TODO: log what happened and why
                Console.Error.WriteLine($"WARN {ex.Message} {{Destination={ex.Destination}}}");
                BadMessageHandler(ex.LookupId, QueueTransaction.Dtc); //TODO: what type is good here?
            }
        }

        int RouteBatchOfMessages()
        {
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
            Message msg = _input.Receive(Properties.All, timeout: TimeSpan.Zero, transaction:QueueTransaction.Dtc);  //note: no waiting
                if (msg == null)
                    return false;

            var dest = GetRoute(msg);

            try
            {                
                dest.Post(msg, QueueTransaction.Dtc); 
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
