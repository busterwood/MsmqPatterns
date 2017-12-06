using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;
using System.Transactions;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages in a transaction. Up to <see cref="MaxBatchSize"/> messages are included in each transaction 
    /// because each transaction has a relatively large overhead.
    /// </summary>
    public abstract class TransactionalRouter : Router
    {
        protected MessageQueue _inProgress;

        protected TransactionalRouter(MessageQueue input, Func<Message, MessageQueue> route)
            : base(input, route)
        {
            Contract.Requires(route != null);
            Contract.Requires(input != null);
        }

        /// <summary>
        /// Maximum number of messages batched into one transaction.
        /// We try to include multiple messages in a batch as it is MUCH faster (up to 10x)
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        public string InProgressSubQueue { get; set; } = "batch";

        protected override Task RunAsync()
        {
            _inProgress = new MessageQueue(_input.FormatName + ";" + InProgressSubQueue, QueueAccessMode.Receive);
            return base.RunAsync();
        }
    }

    /// <summary>Routes batches of messages between local <see cref="MessageQueue"/> using a MSMQ transaction</summary>
    public class MsmqTransactionalRouter : TransactionalRouter
    {        
        public MsmqTransactionalRouter(MessageQueue input, Func<Message, MessageQueue> route)
            : base(input, route)
        {
            Contract.Requires(route != null);
            Contract.Requires(input != null);
            Contract.Requires(input.Transactional);
        }

        protected override void OnNewMessage(Message peeked)
        {
            using (var txn = new MessageQueueTransaction())
            {
                txn.Begin();
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
                    Console.Error.WriteLine($"WARN {ex.Message}S {{Destination={ex.Destination?.FormatName}}}");
                    BadMessageHandler(ex.LookupId, MessageQueueTransactionType.Single);
                }
            }
        }

        int RouteBatchOfMessages(MessageQueueTransaction txn)
        {
            int toSend;
            for (toSend = 0; toSend < MaxBatchSize; toSend++)
            {
                var messages = _input.GetMessageEnumerator2();
                using (var peeked = _input.TryPeek(TimeSpan.Zero))
                {
                    if (peeked == null)
                        break;
                    _input.MoveMessage(InProgressSubQueue, peeked.LookupId, txn);
                }
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

        private bool RouteMessage(MessageQueueTransaction txn)
        {
            using (var msg = _inProgress.TryRecieve(TimeSpan.Zero, txn)) //note: no waiting
            {
                if (msg != null)
                    return false;

                var dest = GetRoute(msg);

                try
                {
                    dest.Send(msg, txn);
                    return true;
                }
                catch (MessageQueueException ex)
                {
                    // we cannot send to that queue
                    throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest);
                }
            }
        }
    }

    /// <summary>Routes batches of messages in local or remote queues using DTC <see cref="TransactionScope"/></summary>
    public class DtcTransactionalRouter : TransactionalRouter
    {

        public DtcTransactionalRouter(MessageQueue input, Func<Message, MessageQueue> route)
            : base(input, route)
        {
            Contract.Requires(input != null);
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
                Console.Error.WriteLine($"WARN {ex.Message} {{Destination={ex.Destination?.FormatName}}}");
                BadMessageHandler(ex.LookupId, MessageQueueTransactionType.Automatic); //TODO: what type is good here?
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
            using (Message msg = _input.TryRecieve(TimeSpan.Zero, MessageQueueTransactionType.Automatic))  //note: no waiting
            {
                if (msg == null)
                    return false;

                var dest = GetRoute(msg);

                try
                {
                    dest.Send(msg, MessageQueueTransactionType.Automatic); 
                    return true;
                }
                catch (MessageQueueException ex)
                {
                    // we cannot send to that queue
                    throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest);
                }
            }
        }
    }
}
