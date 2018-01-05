using System;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    public interface IQueueReader
    {
        /// <summary>Tries to read the current message from the queue without removing the message from the queue.</summary>
        /// <remarks>Within a transaction you cannot peek a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        Message Peek(Properties properties = Properties.All, TimeSpan? timeout = default(TimeSpan?), QueueTransaction transaction = null);

        /// <summary>Tries to peek the current a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        Task<Message> PeekAsync(Properties properties, TimeSpan? timeout = default(TimeSpan?));

        /// <summary>Tries to receive a message from the queue</summary>
        /// <remarks>Within a transaction you cannot receive a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        Message Read(Properties properties = Properties.All, TimeSpan? timeout = default(TimeSpan?), QueueTransaction transaction = null);

        /// <summary>Tries to receive a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="TimeSpan.Zero"/> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        Task<Message> ReadAsync(Properties properties, TimeSpan? timeout = default(TimeSpan?));
    }
}