using System;
using System.ComponentModel;

namespace BusterWood.Msmq
{
    /// <summary>
    /// A transaction to send or receive messages. 
    /// Use <see cref="Single"/> for a transaction that only exists for a single send or receive, 
    /// use <see cref="Dtc"/> to the use ambient DTC transaction.
    /// </summary>
    public class Transaction : IDisposable
    {
        /// <summary>Use the ambient DTC transaction from System.Transactions.TransactionScope</summary>
        public static readonly Transaction Dtc = new SpecialTransaction(1);
        
        /// <summary>An MSMQ transaction that only exists for the duration of a call</summary>
        public static readonly Transaction Single = new SpecialTransaction(3);
        
        internal readonly ITransaction InternalTransaction;
        bool _complete;
        bool _disposed;

        /// <summary>Starts a new MSMQ internal transaction</summary>
        public Transaction()
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

        internal class SpecialTransaction : Transaction
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

    static partial class Extensions
    {
        internal static bool TryGetHandle(this Transaction transaction, out IntPtr handle)
        {
            if (transaction == null)
            {
                handle = IntPtr.Zero;
                return true;
            }
            var fake = transaction as Transaction.SpecialTransaction;
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
