using System.Threading;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>Limit the number of open transactions to avoid swamping MSMQ and bringing the host to it's knees</summary>
    public class LimitedTransaction : QueueTransaction
    {
        /// <summary>The maximum number of open transactions that are allowed</summary>
        public static int MaxOpenTransactions { get; set; } = 5000;

        static readonly object _gate = new object();
        static int _open;

        /// <summary>Opens a transaction, possibly waiting if the maximum number of open transactions has been reached</summary>
        public LimitedTransaction()
        {
            lock (_gate)
            {
                while (_open >= MaxOpenTransactions)
                {
                    Monitor.Wait(_gate);
                }
                _open++;
            }
        }

        /// <summary>Rollback the transaction</summary>
        public override void Abort()
        {
            base.Abort();
            ReduceOpenCount();
        }

        /// <summary>Commit the transaction</summary>
        public override void Commit()
        {
            base.Commit();
            ReduceOpenCount();
        }

        /// <summary>Aborts the transaction if <see cref="M:BusterWood.Msmq.QueueTransaction.Commit" /> or <see cref="M:BusterWood.Msmq.QueueTransaction.Abort" /> has not already been called</summary>
        public override void Dispose()
        {
            if (_disposed || _complete) return;
            base.Dispose();
            ReduceOpenCount();
        }

        static void ReduceOpenCount()
        {
            lock (_gate)
            {
                _open--;
                Monitor.Pulse(_gate);
            }
        }
    }
}
