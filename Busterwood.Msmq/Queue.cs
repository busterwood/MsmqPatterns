using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    /// <summary>Represents a message queue</summary>
    public abstract class Queue : IDisposable
    {
        internal QueueHandle _handle;

        /// <summary>Gets the full format name of this queue</summary>
        public string FormatName { get; private set; }

        /// <summary>How this queue was opened</summary>
        public QueueAccessMode AccessMode { get; }

        /// <summary>How the queue is shared</summary>
        public QueueShareReceive ShareMode { get; }

        /// <summary>Has the queue been closed? (or disposed)</summary>
        public bool IsClosed => _handle == null || _handle.IsClosed;

        internal Queue(string formatName, QueueAccessMode accessMode, QueueShareReceive shareMode)
        {
            Contract.Requires(formatName != null);
            AccessMode = accessMode;
            ShareMode = shareMode;
            FormatName = formatName;
        }

        /// <summary>Opens the queue</summary>
        internal void Open()
        {
            if (!IsClosed)
                _handle.Close();

            int res = Native.OpenQueue(FormatName, AccessMode, ShareMode, out _handle);
            if (res != 0)
                throw new QueueException(res);

            FormatName = FormatNameFromHandle(); // gets the full DNS format name
        }

        /// <summary>Closes this queue</summary>
        public virtual void Dispose()
        {
            if (IsClosed) return;
            _handle.Dispose();
        }

        string FormatNameFromHandle()
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

            int size = 255;
            var sb = new StringBuilder(size);

            int res = Native.HandleToFormatName(_handle, sb, ref size);
            if (res != 0)
                throw new QueueException(res);

            sb.Length = size - 1; // remove null terminator
            return sb.ToString();
        }

        public override string ToString() => FormatName;

        /// <summary>Creates a message queue (if it does not already exist), returning the format name of the queue.</summary>
        /// <param name="path">the path (NOT format name) of the queue</param>
        /// <param name="transactional">create a transactional queue or not?</param>
        public static string TryCreate(string path, QueueTransactional transactional)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(path));
            Contract.Ensures(Contract.Result<string>() != null);

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
            Contract.Requires(path != null);

            int size = 255;
            var sb = new StringBuilder(size);
            int res = Native.PathNameToFormatName(path, sb, ref size);
            if ((ErrorCode)res == ErrorCode.QueueNotFound)
                return false;

            if (res != 0)
                throw new QueueException(res);

            return true;
        }

        /// <summary>Returns the transactional property of the queue</summary>
        public static QueueTransactional IsTransactional(string formatName)
        {
            Contract.Requires(formatName != null);
            var props = new QueueProperties();
            props.SetByte(Native.QUEUE_PROPID_TRANSACTION, 0);
            int status = Native.GetQueueProperties(formatName, props.Allocate());
            props.Free();
            if (Native.IsError(status))
                throw new QueueException(status);

            return (QueueTransactional)props.GetByte(Native.QUEUE_PROPID_TRANSACTION);
        }

        /// <summary>Move the message specified by <paramref name="lookupId"/> from <paramref name="sourceQueue"/> to the <paramref name="targetQueue"/>.</summary>
        /// <remarks>
        /// Moving message is 10 to 100 times faster than sending the message to another queue.
        /// Within a transaction you cannot receive a message that you moved to a subqueue.
        /// </remarks>
        public static void MoveMessage(QueueReader sourceQueue, SubQueue targetQueue, long lookupId, QueueTransaction transaction = null)
        {
            Contract.Requires(sourceQueue != null);
            Contract.Requires(targetQueue != null);

            if (sourceQueue.IsClosed) throw new ObjectDisposedException(nameof(sourceQueue));
            if (targetQueue.IsClosed) throw new ObjectDisposedException(nameof(targetQueue));

            int res;
            IntPtr txnHandle;
            if (transaction.TryGetHandle(out txnHandle))
                res = Native.MoveMessage(sourceQueue._handle, targetQueue.MoveHandle, lookupId, txnHandle);
            else
                res = Native.MoveMessage(sourceQueue._handle, targetQueue.MoveHandle, lookupId, transaction.InternalTransaction);

            if (Native.IsError(res))
                throw new QueueException(res);
        }
    }

    /// <summary>Class that represents a message queue that you can post messages to</summary>
    public class QueueWriter : Queue
    {
        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="Queue.PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
        public QueueWriter(string formatName) 
            : base(formatName, QueueAccessMode.Send, QueueShareReceive.Shared)
        {
            Open();
        }

        /// <summary>
        /// Asks MSMQ to attempt to deliver a message.
        /// To ensure the message reached the queue you need to check acknowledgement messages sent to the <see cref="Message.AdministrationQueue"/>
        /// </summary>
        /// <param name="message">The message to try to send</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        public void Write(Message message, QueueTransaction transaction = null)
        {
            Contract.Requires(message != null);

            message.Props.PrepareToSend();
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

    }

    /// <summary>Reads messages from a queue</summary>
    public class QueueReader : Queue, IQueueReader
    {
        /// <summary>The maximum amount of time for queue operations</summary>
        public static TimeSpan Infinite = TimeSpan.FromMilliseconds(uint.MaxValue);

        readonly HashSet<QueueAsyncRequest> _outstanding = new HashSet<QueueAsyncRequest>();
        bool _boundToThreadPool;

        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="Queue.PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
        public QueueReader(string formatName, QueueReaderMode readerMode = QueueReaderMode.Receive, QueueShareReceive share = QueueShareReceive.Shared)
            : base(formatName, (QueueAccessMode)readerMode, share)
        {
            Open();
        }

        /// <summary>
        /// Sends a negative acknowledgement of <see cref="MessageClass.ReceiveRejected"/> to the <see cref="Message.AdministrationQueue"/> when the transaction is committed.
        /// NOTE: Must be called in the scope of a the message MUST have been received in the scope of a transaction.
        /// </summary>
        public void MarkRejected(long lookupId)
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

            int res = Native.MarkMessageRejected(_handle, lookupId);
            if (Native.IsError(res))
                throw new QueueException(res);
        }

        /// <summary>Tries to peek the current a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> PeekAsync(Properties properties, TimeSpan? timeout = null)
        {
            return ReceiveAsync(properties, ReadAction.PeekCurrent, timeout, CursorHandle.None);
        } 
        
        /// <summary>Tries to receive a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> ReadAsync(Properties properties, TimeSpan? timeout = null)
        {
            return ReceiveAsync(properties, ReadAction.Receive, timeout, CursorHandle.None);
        }

        internal Task<Message> ReceiveAsync(Properties properties, ReadAction action, TimeSpan? timeout, CursorHandle cursor)
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

            uint timeoutMS = TimeoutInMs(timeout);
            var msg = new Message();
            msg.Props.SetForRead(properties);
            var ar = new QueueAsyncRequest(msg, _outstanding, timeoutMS, _handle, action, cursor);

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

        /// <summary>Tries to read the current message from the queue without removing the message from the queue.</summary>
        /// <remarks>Within a transaction you cannot peek a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message Peek(Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            return Receive(properties, ReadAction.PeekCurrent, timeout, transaction, CursorHandle.None);
        }

        /// <summary>Tries to receive a message from the queue</summary>
        /// <remarks>Within a transaction you cannot receive a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message Read(Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            return Receive(properties, ReadAction.Receive, timeout, transaction, CursorHandle.None);
        }

        internal unsafe Message Receive(Properties properties, ReadAction action, TimeSpan? timeout, QueueTransaction transaction, CursorHandle cursor)
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

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
                        res = Native.ReceiveMessage(_handle, timeoutMS, action, props, null, null, cursor, txnHandle);
                    else
                        res = Native.ReceiveMessage(_handle, timeoutMS, action, props, null, null, cursor, transaction.InternalTransaction);
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
        /// <remarks>Within a transaction you cannot receive a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="lookupId">The <see cref="Message.LookupId"/> of the message to read</param>
        /// <param name="action">Receive or peek a message?</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the message was not found or the receive times out</returns>
        public unsafe Message Lookup(Properties properties, long lookupId, LookupAction action = LookupAction.ReceiveCurrent, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

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

                if ((ErrorCode)res == ErrorCode.IOTimeout || (ErrorCode)res == ErrorCode.MessageNotFound)
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

        internal uint TimeoutInMs(TimeSpan? timeout)
        {
            double ms = (timeout ?? Infinite).TotalMilliseconds;
            uint timeoutMS = (ms > uint.MaxValue) ? uint.MaxValue : (uint)ms;
            return timeoutMS;
        }

        /// <summary>Deletes all the messages in the queue.  Note: the queue must be opened with <see cref="QueueAccessMode.Receive"/></summary>
        public void Purge()
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

            int res = Native.PurgeQueue(_handle);
            if (res != 0)
                throw new QueueException(res);
        }

    }

    /// <summary>A sub-queue that you can peek and read from, but also move message to via <see cref="Queue.MoveMessage(QueueReader, SubQueue, long, QueueTransaction)"/></summary>
    public class SubQueue : QueueReader
    {
        private QueueHandle _moveHandle;

        internal QueueHandle MoveHandle => _moveHandle;

        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="Queue.PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
        public SubQueue(string formatName, QueueReaderMode mode = QueueReaderMode.Receive, QueueShareReceive share = QueueShareReceive.Shared)
            : base(formatName, mode, share)
        {
            Contract.Requires(formatName.IndexOf(';') > 0, "formatName is not a subqueue");

            int res = Native.OpenQueue(FormatName, QueueAccessMode.Move, QueueShareReceive.Shared, out _moveHandle);
            if (res != 0)
                throw new QueueException(res);
        }

        public override void Dispose()
        {
            base.Dispose();
            _moveHandle?.Dispose();
        }
    }
}
