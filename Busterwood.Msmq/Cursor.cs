using System.Diagnostics.Contracts;
using System;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    /// <summary>Use can use a cursor to iterate over the messages in a queue</summary>
    public class Cursor : IDisposable, IQueueReader
    {
        readonly QueueReader _reader;
        readonly CursorHandle _cursorHandle;

        public Cursor(QueueReader reader)
        {
            Contract.Requires(reader != null);
            _reader = reader;

            int res = Native.CreateCursor(reader._handle, out _cursorHandle);
            if (Native.IsError(res))
                throw new QueueException(res);
        }

        public void Dispose()
        {
            _cursorHandle.Dispose();
        }

        /// <summary>Tries to peek the current a message from the cursor, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> PeekAsync(Properties properties, TimeSpan? timeout = null)
        {
            return _reader.ReceiveAsync(properties, ReadAction.PeekCurrent, timeout, _cursorHandle);
        }

        /// <summary>Tries to peek the next message from the cursor, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> PeekNextAsync(Properties properties, TimeSpan? timeout = null)
        {
            return _reader.ReceiveAsync(properties, ReadAction.PeekNext, timeout, _cursorHandle);
        }

        /// <summary>Tries to read the current message from the cursor without removing the message from the queue.</summary>
        /// <remarks>Within a transaction you cannot peek a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message Peek(Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            return _reader.Receive(properties, ReadAction.PeekCurrent, timeout, transaction, _cursorHandle);
        }

        /// <summary>Tries to read the next message from the cursor without removing the message from the queue.</summary>
        /// <remarks>Within a transaction you cannot peek a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message PeekNext(Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            return _reader.Receive(properties, ReadAction.PeekNext, timeout, transaction, _cursorHandle);
        }

        /// <summary>Tries to receive a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> ReadAsync(Properties properties, TimeSpan? timeout = null)
        {
            return _reader.ReceiveAsync(properties, ReadAction.Receive, timeout, _cursorHandle);
        }

        /// <summary>Tries to receive a message from the queue</summary>
        /// <remarks>Within a transaction you cannot receive a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message Read(Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            return _reader.Receive(properties, ReadAction.Receive, timeout, transaction, _cursorHandle);
        }
    }
}