﻿using System;
using System.ComponentModel;
using System.Threading;

namespace BusterWood.Msmq
{
    /// <summary>
    /// A transaction to send or receive messages. 
    /// Use <see cref="Single"/> for a transaction that only exists for a single send or receive, 
    /// use <see cref="Dtc"/> to the use ambient DTC transaction.
    /// </summary>
    public class QueueTransaction : IDisposable
    {
        public static readonly QueueTransaction None = null;

        /// <summary>Use the ambient DTC transaction from System.Transactions.TransactionScope</summary>
        public static readonly QueueTransaction Dtc = new SpecialTransaction(1);
        
        /// <summary>An MSMQ transaction that only exists for the duration of a call</summary>
        public static readonly QueueTransaction Single = new SpecialTransaction(3);
        
        internal readonly ITransaction InternalTransaction;
        internal bool _complete;
        internal bool _disposed;

        /// <summary>Starts a new MSMQ internal transaction</summary>
        public QueueTransaction()
        {
            int res = Native.BeginTransaction(out InternalTransaction);
            if (res != 0)
                throw new Win32Exception(res); //TODO: some other type of exception?
        }

        /// <summary>Commit the transaction</summary>
        public virtual void Commit()
        {
            if (_disposed) throw new ObjectDisposedException("MSMQ Transaction");
            int res = InternalTransaction.Commit(0, 0, 0);
            if (res != 0)
                throw new Win32Exception(res); //TODO: some other type of exception?
            _complete = true;
        }

        /// <summary>Rollback the transaction</summary>
        public virtual void Abort()
        {
            if (_disposed) throw new ObjectDisposedException("MSMQ Transaction");
            int res = InternalTransaction.Abort(0, 0, 0);
            if (res != 0)
                throw new Win32Exception(res); //TODO: some other type of exception?
            _complete = true;
        }

        /// <summary>Aborts the transaction if <see cref="Commit"/> or <see cref="Abort"/> has not already been called</summary>
        public virtual void Dispose()
        {
            if (_disposed || _complete) return;
            InternalTransaction.Abort(0, 0, 0); // don't check for errors or throw in dispose method
            _disposed = true;
            _complete = true;
        }

        internal class SpecialTransaction : QueueTransaction
        {
            public IntPtr SpecialId { get; }

            public SpecialTransaction(int specialId)
            {
                SpecialId = (IntPtr)specialId;
            }

            public override void Commit()
            {
                throw new NotImplementedException();
            }

            public override void Abort()
            {
                throw new NotImplementedException();
            }

            public override void Dispose()
            {
            }
        }
    }

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

    static partial class Extensions
    {
        internal static bool TryGetHandle(this QueueTransaction transaction, out IntPtr handle)
        {
            if (transaction == null)
            {
                handle = IntPtr.Zero;
                return true;
            }
            var fake = transaction as QueueTransaction.SpecialTransaction;
            if (fake != null)
            {
                handle = fake.SpecialId;
                return true;
            }
            handle = IntPtr.Zero;
            return false;
        }
    }
}
