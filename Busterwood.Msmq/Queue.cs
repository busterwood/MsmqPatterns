using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Text;

namespace Busterwood.Msmq
{
    public class Queue : IDisposable
    {
        public static TimeSpan Infinite = TimeSpan.FromMilliseconds(uint.MaxValue);
        readonly QueueHandle _handle;
        bool _closed;

        public string Name { get; } //TODO: do we need this?  just use the FormatName property?

        //TODO: different classes for Send and recieve?

        /// <summary>Opens a queue using a format name</summary>
        public static Queue Open(string formatName, QueueAccessMode mode, QueueShareMode share = QueueShareMode.Shared)
        {
            Contract.Requires(formatName != null);
            Contract.Ensures(Contract.Result<Queue>() != null);

            QueueHandle handle;
            int res = Native.OpenQueue(formatName, mode, share, out handle);
            if (res != 0)
                throw new Win32Exception(res);
            return new Queue(formatName, handle);
        }

        /// <summary>converts a queue path to a format name</summary>
        public static string PathToFormatName(string path)
        {
            int size = 255;
            var sb = new StringBuilder(size);
            int res = Native.PathNameToFormatName(path, sb, ref size);
            if (res != 0)
                throw new Win32Exception(res);
            sb.Length = size - 1;
            return sb.ToString();
        }

        private Queue(string name, QueueHandle handle)
        {
            Contract.Requires(name != null);
            Contract.Requires(handle != null);
            Name = name;
            _handle = handle;
        }

        /// <summary>Closes this queue</summary>
        public void Close()
        {
            if (_closed) return;
            _handle.Dispose();
            _closed = true;
        }

        /// <summary>Gets the full format name of this queue</summary>
        public string FormatName()
        {
            if (_closed) throw new ObjectDisposedException(Name);

            int size = 255;
            var sb = new StringBuilder(size);

            int res = Native.HandleToFormatName(_handle, sb, ref size);
            if (res != 0)
                throw new Win32Exception(res);

            sb.Length = size - 1; // remove null terminator
            return sb.ToString();
        }

        /// <summary>
        /// Asks MSMQ to attempt to deliver a message.
        /// To ensure the message reached the queue you need to check acknowledgement messages sent to the <see cref="Message.AdministrationQueue"/>
        /// </summary>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="Transaction"/>, <see cref="Transaction.Single"/>, or <see cref="Transaction.Dtc"/>.</param>
        public void Post(Message message, Transaction transaction = null)
        {
            Contract.Requires(message != null);

            var props = message.Props.Allocate();
            try
            {
                int res;
                IntPtr txnHandle;
                if (transaction.TryGetHandle(out txnHandle))
                    res = Native.SendMessage(_handle, props, txnHandle);
                else
                    res = Native.SendMessage(_handle, props, transaction.InternalTransaction);

                if (IsError(res))
                    throw new Win32Exception(res);
            }
            finally
            {
                message.Props.Free();
            }
        }

        /// <summary>
        /// Tries to receive a message from the queue.
        /// </summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="action">Receive or peek a message?</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="Transaction"/>, <see cref="Transaction.Single"/>, or <see cref="Transaction.Dtc"/>.</param>
        /// <returns></returns>
        public Message Receive(Properties properties, QueueAction action = QueueAction.Receive, TimeSpan? timeout = null, Transaction transaction = null)
        {
            double ms = (timeout ?? Infinite).TotalMilliseconds;
            uint timeoutMS = (ms > uint.MaxValue) ? uint.MaxValue : (uint)ms;
            var msg = new Message();
            int res;

            msg.Props.SetForRead(properties);
            for (;;) // loop because we might need to adjust memory size
            {
                var props = msg.Props.Allocate();
                try
                {
                    unsafe
                    {
                        IntPtr txnHandle;
                        if (transaction.TryGetHandle(out txnHandle))
                            res = Native.ReceiveMessage(_handle, timeoutMS, action, props, null, null, CursorHandle.None, txnHandle);
                        else
                            res = Native.ReceiveMessage(_handle, timeoutMS, action, props, null, null, CursorHandle.None, transaction.InternalTransaction);
                    }
                }
                finally
                {
                    msg.Props.Free();
                }

                if (NotEnoughMemory(res))
                {
                    msg.Props.AdjustMemory();
                    continue; // try again
                }

                if (IsError(res))
                    throw new Win32Exception(res);

                return msg;
            }
        }

        static bool NotEnoughMemory(int value)
        {
            return (value == (int)ErrorCode.BufferOverflow ||
                 value == (int)ErrorCode.LabelBufferTooSmall ||
                 value == (int)ErrorCode.ProviderNameBufferTooSmall ||
                 value == (int)ErrorCode.SenderCertificateBufferTooSmall ||
                 value == (int)ErrorCode.SenderIdBufferTooSmall ||
                 value == (int)ErrorCode.SecurityDescriptorBufferTooSmall ||
                 value == (int)ErrorCode.SignatureBufferTooSmall ||
                 value == (int)ErrorCode.SymmetricKeyBufferTooSmall ||
                 value == (int)ErrorCode.UserBufferTooSmall ||
                 value == (int)ErrorCode.FormatNameBufferTooSmall);
        }

        public void Dispose()
        {
            Close();
        }

        internal static bool IsError(int hresult)
        {
            bool isSuccessful = (hresult == 0x00000000);
            bool isInformation = ((hresult & unchecked((int)0xC0000000)) == 0x40000000);
            return (!isInformation && !isSuccessful);
        }

    }
}
