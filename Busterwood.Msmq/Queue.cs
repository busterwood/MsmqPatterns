using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    /// <summary>An MSMQ message queue.  Call <see cref="Open(string, QueueAccessMode, QueueShareMode)"/> to open a message queue.</summary>
    public class Queue : IDisposable
    {
        /// <summary>The maximum amount of time for queue operations</summary>
        public static TimeSpan Infinite = TimeSpan.FromMilliseconds(uint.MaxValue);

        readonly HashSet<QueueAsyncRequest> _outstanding = new HashSet<QueueAsyncRequest>();
        bool _boundToThreadPool;
        readonly QueueHandle _handle;
        string _formatName;
        bool _closed;

        public QueueAccessMode Access { get; }

        private Queue(QueueHandle handle, string formatName, QueueAccessMode access)
        {
            Contract.Requires(handle != null);
            _handle = handle;
            Access = access;
        }

        /// <summary>Closes this queue</summary>
        public void Close()
        {
            if (_closed) return;
            _handle.Dispose();
            _closed = true;
        }

        /// <summary>Gets the full format name of this queue</summary>
        public string FormatName => (_formatName ?? (_formatName = FormatNameFromHandle()));

        string FormatNameFromHandle()
        {
            if (_closed) throw new ObjectDisposedException(nameof(Queue));

            int size = 255;
            var sb = new StringBuilder(size);

            int res = Native.HandleToFormatName(_handle, sb, ref size);
            if (res != 0)
                throw new QueueException(res);

            sb.Length = size - 1; // remove null terminator
            return sb.ToString();
        }

        /// <summary>
        /// Asks MSMQ to attempt to deliver a message.
        /// To ensure the message reached the queue you need to check acknowledgement messages sent to the <see cref="Message.AdministrationQueue"/>
        /// </summary>
        /// <param name="message">The message to try to send</param>
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

                if (Native.IsError(res))
                    throw new QueueException(res);
            }
            finally
            {
                message.Props.Free();
            }
        }

        /// <summary>Tries to receive a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="action">Receive or peek a message?</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> ReceiveAsync(Properties properties, ReceiveAction action = ReceiveAction.Receive, TimeSpan? timeout = null)
        {
            uint timeoutMS = TimeoutInMs(timeout);
            var msg = new Message();
            msg.Props.SetForRead(properties);
            var ar = new QueueAsyncRequest(msg, _outstanding, timeoutMS, _handle, action);

            lock (_outstanding)
            {
                _outstanding.Add(ar); // hold a reference to prevent objects being collected
                if (!_boundToThreadPool)
                {
                    ThreadPool.BindHandle(_handle); // queue can now use IO completion port
                    _boundToThreadPool = true;
                }
            }

            return ar.ReceiveAsync();
        }

        /// <summary>Tries to receive a message from the queue</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="action">Receive or peek a message?</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="Transaction"/>, <see cref="Transaction.Single"/>, or <see cref="Transaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public unsafe Message Receive(Properties properties, ReceiveAction action = ReceiveAction.Receive, TimeSpan? timeout = null, Transaction transaction = null)
        {
            uint timeoutMS = TimeoutInMs(timeout);
            var msg = new Message();
            int res;

            msg.Props.SetForRead(properties);
            for (;;) // loop because we might need to adjust memory size
            {
                var props = msg.Props.Allocate();
                try
                {
                    IntPtr txnHandle;
                    if (transaction.TryGetHandle(out txnHandle))
                        res = Native.ReceiveMessage(_handle, timeoutMS, action, props, null, null, CursorHandle.None, txnHandle);
                    else
                        res = Native.ReceiveMessage(_handle, timeoutMS, action, props, null, null, CursorHandle.None, transaction.InternalTransaction);
                }
                finally
                {
                    msg.Props.Free();
                }

                if ((ErrorCode)res == ErrorCode.IOTimeout)
                    return null;

                if (Native.NotEnoughMemory(res))
                {
                    msg.Props.AdjustMemory();
                    continue; // try again
                }

                if (Native.IsError(res))
                    throw new QueueException(res);

                return msg;
            }
        }

        /// <summary>Tries to peek (or receive) a message using the queue-specific <paramref name="lookupId"/></summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="lookupId">The <see cref="Message.LookupId"/> of the message to read</param>
        /// <param name="action">Receive or peek a message?</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="Transaction"/>, <see cref="Transaction.Single"/>, or <see cref="Transaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public unsafe Message Receive(Properties properties, long lookupId, LookupAction action = LookupAction.ReceiveCurrent, TimeSpan? timeout = null, Transaction transaction = null)
        {
            uint timeoutMS = TimeoutInMs(timeout);
            var msg = new Message();
            int res;

            msg.Props.SetForRead(properties);
            for (;;) // loop because we might need to adjust memory size
            {
                var props = msg.Props.Allocate();
                try
                {
                    IntPtr txnHandle;
                    if (transaction.TryGetHandle(out txnHandle))
                        res = Native.ReceiveMessageByLookupId(_handle, lookupId, action, props, null, null, txnHandle);
                    else
                        res = Native.ReceiveMessageByLookupId(_handle, lookupId, action, props, null, null, transaction.InternalTransaction);
                }
                finally
                {
                    msg.Props.Free();
                }

                if ((ErrorCode)res == ErrorCode.IOTimeout)
                    return null;

                if (Native.NotEnoughMemory(res))
                {
                    msg.Props.AdjustMemory();
                    continue; // try again
                }

                if (Native.IsError(res))
                    throw new QueueException(res);

                return msg;
            }
        }

        public override string ToString() => FormatName;

        private static uint TimeoutInMs(TimeSpan? timeout)
        {
            double ms = (timeout ?? Infinite).TotalMilliseconds;
            uint timeoutMS = (ms > uint.MaxValue) ? uint.MaxValue : (uint)ms;
            return timeoutMS;
        }

        /// <summary>Move the message specified by <paramref name="lookupId"/> to the <paramref name="subQueue"/></summary>
        public void Move(long lookupId, Queue subQueue, Transaction transaction = null)
        {
            Contract.Requires(subQueue != null);
            int res;
            IntPtr txnHandle;
            if (transaction.TryGetHandle(out txnHandle))
                res = Native.MoveMessage(_handle, subQueue._handle, lookupId, txnHandle);
            else
                res = Native.MoveMessage(_handle, subQueue._handle, lookupId, transaction.InternalTransaction);

            if (Native.IsError(res))
                throw new QueueException(res);
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>Creates a message queue (if it does not already exist), returning the format name of the queue.</summary>
        /// <param name="path">the path (NOT format name) of the queue</param>
        /// <param name="transactional">create a transactional queue or not?</param>
        public static string TryCreate(string path, QueueTransactional transactional)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(path));
            const int MaxLabelLength = 124;

            //Create properties.
            var properties = new QueueProperties();
            properties.SetString(Native.QUEUE_PROPID_PATHNAME, Message.StringToBytes(path));
            properties.SetByte(Native.QUEUE_PROPID_TRANSACTION, (byte)transactional);

            StringBuilder formatName = new StringBuilder(MaxLabelLength);
            int len = MaxLabelLength;

            //Try to create queue.
            int res = Native.CreateQueue(IntPtr.Zero, properties.Allocate(), formatName, ref len);
            properties.Free();

            if ((ErrorCode)res == ErrorCode.QueueExists)
                return PathToFormatName(path);

            if (Native.IsError(res))
                throw new QueueException(res);

            formatName.Length = len;
            return formatName.ToString();
        }

        /// <summary>Tries to delete an existing message queue, returns TRUE if the queue was deleted, FALSE if the queue does not exists</summary>
        /// <param name="formatName">The format name (NOT path name) of the queue</param>
        public static bool TryDelete(string formatName)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(formatName));

            int res = Native.DeleteQueue(formatName);

            if ((ErrorCode)res == ErrorCode.QueueNotFound)
                return false;

            if (Native.IsError(res))
                throw new QueueException(res);

            return true;
        }

        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
        public static Queue Open(string formatName, QueueAccessMode mode, QueueShareMode share = QueueShareMode.Shared)
        {
            Contract.Requires(formatName != null);
            Contract.Ensures(Contract.Result<Queue>() != null);

            QueueHandle handle;
            int res = Native.OpenQueue(formatName, mode, share, out handle);
            if (res != 0)
                throw new QueueException(res);
            return new Queue(handle, formatName, mode);
        }

        /// <summary>converts a queue path to a format name</summary>
        public static string PathToFormatName(string path)
        {
            int size = 255;
            var sb = new StringBuilder(size);
            int res = Native.PathNameToFormatName(path, sb, ref size);
            if (res != 0)
                throw new QueueException(res);
            sb.Length = size - 1;
            return sb.ToString();
        }

        /// <summary>Tests if a queue existing. Does NOT accept format names</summary>
        public static bool Exists(string path)
        {
            int size = 255;
            var sb = new StringBuilder(size);
            int res = Native.PathNameToFormatName(path, sb, ref size);
            if ((ErrorCode)res == ErrorCode.QueueNotFound)
                return false;

            if (res != 0)
                throw new QueueException(res);

            return true;
        }

    }
}
