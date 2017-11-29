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

        protected override async Task Run()
        {
            while (!_stop)
            {
                using (Message peeked = await PeekAsync())
                {
                    if (peeked != null)
                        OnNewMessage(peeked);
                }
            }
        }

        async Task<Message> PeekAsync()
        {
            var current = _input.MessageReadPropertyFilter; // save filter so it can be restored after peek
            try
            {
                _input.MessageReadPropertyFilter = PeekFilter;
                return await _input.PeekAsync(StopTime);
            }
            finally
            {
                _input.MessageReadPropertyFilter = current; // restore filter
            }
        }

        protected abstract void OnNewMessage(Message peeked);
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

        protected override void OnNewMessage(Message peeked)
        {
            using (var txn = new MessageQueueTransaction())
            {
                txn.Begin();

                using (Message msg = _input.RecieveWithTimeout(TimeSpan.Zero, txn))
                {
                    if (msg == null) // message has been received by another process or thread
                        return;

                    TryToRoute(msg, txn);
                    msg.Dispose(); // release early

                    // try to include multiple messages in a batch as it is MUCH faster (up to 10x)
                    for (int i = 1; i < MaxBatchSize; i++)
                    {
                        using (Message msg2 = _input.RecieveWithTimeout(TimeSpan.Zero, txn))
                        {
                            if (msg2 == null) // message has been received by another process or thread
                                break;
                            TryToRoute(msg2, txn);
                        }
                    }
                }

                txn.Commit();
            }
        }

        private void TryToRoute(Message msg, MessageQueueTransaction txn)
        {
            var dest = _route(msg) ?? _deadLetter; //TODO: what if route fails?
            dest.Send(msg, txn); // TODO: what if we cannot send?
        }
    }

    /// <summary>Routes messages in local or remote queues using DTC <see cref="TransactionScope"/></summary>
    public class DtcTransactionalRouter : TransactionalRouter
    {
        public DtcTransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
            : base(input, deadletter, route)
        {
        }

        protected override void OnNewMessage(Message peeked)
        {
            using (var txn = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                using (Message msg = _input.RecieveWithTimeout(TimeSpan.Zero, MessageQueueTransactionType.Automatic))
                {
                    if (msg == null) // message has been received by another process or thread
                        return;

                    TryToRoute(msg);
                    msg.Dispose(); // release early

                    // try to include multiple messages in a batch as it is MUCH faster (up to 10x)
                    for (int i = 1; i < MaxBatchSize; i++)
                    {
                        using (Message msg2 = _input.RecieveWithTimeout(TimeSpan.Zero, MessageQueueTransactionType.Automatic))
                        {
                            if (msg2 == null) // message has been received by another process or thread
                                break;
                            TryToRoute(msg2);
                        }
                    }
                }

                txn.Complete(); //TODO: handle commit exceptions
            }
        }

        private void TryToRoute(Message msg)
        {
            var dest = _route(msg) ?? _deadLetter; //TODO: what if route fails?
            dest.Send(msg, MessageQueueTransactionType.Automatic); // TODO: what if we cannot send?
        }
    }
}
