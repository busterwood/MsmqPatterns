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
        protected TransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
            : base(input, deadletter, route)
        {
        }

        /// <summary>
        /// Maximum number of messages batched into one transaction.
        /// We try to include multiple messages in a batch as it is MUCH faster (up to 10x)
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        protected override async Task RunAsync()
        {
            while (!_stop)
            {
                bool got = await NewMessageAsync();
                if (got)
                    OnNewMessage();
            }
        }

        async Task<bool> NewMessageAsync()
        {
            var current = _input.MessageReadPropertyFilter; // save filter so it can be restored after peek
            try
            {
                _input.MessageReadPropertyFilter = PeekFilter;
                using (var msg = await _input.PeekAsync(StopTime))
                {
                    return msg != null;
                }
            }
            finally
            {
                _input.MessageReadPropertyFilter = current; // restore filter
            }
        }

        protected abstract void OnNewMessage();
    }

    /// <summary>Routes messages between local <see cref="MessageQueue"/> using a local MSMQ transaction</summary>
    public class MsmqTransactionalRouter : TransactionalRouter
    {
        public MsmqTransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
            : base(input, deadletter, route)
        {
            Contract.Requires(input.Transactional);
            Contract.Requires(deadletter.Transactional);
        }

        protected override void OnNewMessage()
        {
            using (var txn = new MessageQueueTransaction())
            {
                txn.Begin();
                int count = RouteBatchOfMessages(txn);
                if (count > 0)
                    txn.Commit();
            }
        }

        int RouteBatchOfMessages(MessageQueueTransaction txn)
        {
            int count = 0;
            for (int i = 0; i < MaxBatchSize; i++)
            {
                using (Message msg = _input.RecieveWithTimeout(TimeSpan.Zero, txn)) //note: no waiting
                {
                    if (msg == null)
                        break;

                    var dest = GetRoute(msg);
                    dest.Send(msg, txn); // TODO: what if we cannot send?
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>Routes messages in local or remote queues using DTC <see cref="TransactionScope"/></summary>
    public class DtcTransactionalRouter : TransactionalRouter
    {
        public DtcTransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
            : base(input, deadletter, route)
        {
        }

        protected override void OnNewMessage()
        {
            using (var txn = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                int count = RouteBatchOfMessages();
                if (count > 0)
                    txn.Complete();
            }
        }

        int RouteBatchOfMessages()
        {
            int count = 0;
            for (int i = 0; i < MaxBatchSize; i++)
            {
                using (Message msg = _input.RecieveWithTimeout(TimeSpan.Zero, MessageQueueTransactionType.Automatic))  //note: no waiting
                {
                    if (msg == null)
                        break;

                    var dest = GetRoute(msg);
                    dest.Send(msg, MessageQueueTransactionType.Automatic); // TODO: what if we cannot send?
                    count++;
                }
            }
            return count;
        }
    }
}
