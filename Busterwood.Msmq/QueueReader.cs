using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{

    /// <summary>Reads messages from a queue</summary>
    public class QueueReader : Queue, IQueueReader, IEnumerable<Message>
    {
        /// <summary>The maximum amount of time for queue operations</summary>
        public static TimeSpan Infinite = TimeSpan.FromMilliseconds(uint.MaxValue);

        readonly HashSet<QueueAsyncRequest> _outstanding = new HashSet<QueueAsyncRequest>();
        bool _boundToThreadPool;

        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="Queues.PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
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
                    msg.Props.IncreaseBufferSize();
                    continue; // try again
                }

                if (Native.IsError(res))
                    throw new QueueException(res);

                msg.Props.ResizeBody();
                return msg;
            }
        }

        /// <summary>Tries to peek (or receive) a message using the queue-specific <paramref name="lookupId"/></summary>
        /// <remarks>Within a transaction you cannot receive a message that you moved to a subqueue within the same transaction</remarks>
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
                    msg.Props.IncreaseBufferSize();
                    continue; // try again
                }

                if (Native.IsError(res))
                    throw new QueueException(res);

                msg.Props.ResizeBody();
                return msg;
            }
        }

        internal uint TimeoutInMs(TimeSpan? timeout)
        {
            double ms = (timeout ?? Infinite).TotalMilliseconds;
            return (ms > uint.MaxValue) ? uint.MaxValue : (uint)ms;
        }

        /// <summary>Deletes all the messages in the queue.  Note: the queue must be opened with <see cref="QueueAccessMode.Receive"/></summary>
        public void Purge()
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

            int res = Native.PurgeQueue(_handle);
            if (res != 0)
                throw new QueueException(res);
        }

        /// <summary>Peeks at all the messages currently in the queue using a <see cref="QueueCursor"/></summary>
        public IEnumerator<Message> GetEnumerator()
        {
            using (var c = new QueueCursor(this))
            {
                for (var msg = c.Peek(timeout:TimeSpan.Zero); msg != null; msg = c.PeekNext(timeout: TimeSpan.Zero))
                {
                    yield return msg;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
