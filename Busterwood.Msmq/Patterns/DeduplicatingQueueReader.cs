using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;

namespace BusterWood.Msmq.Patterns
{
    //TOOD: what if a message was read in a transaction that was then aborted?

    /// <summary>Prevents duplicate messages being read (not peeked) for the same label</summary>
    public class DeduplicatingQueueReader : IQueueReader, IDisposable
    {
        readonly IQueueReader _queueReader;
        readonly Cache<string, string> _hashByLabel;
        readonly SHA1 _hash;

        public DeduplicatingQueueReader(IQueueReader queueReader, int? gen0Limit = null, TimeSpan? timeToLive = null)
        {
            Contract.Requires(queueReader != null);
            _queueReader = queueReader;
            _hashByLabel = new Cache<string, string>(gen0Limit, timeToLive ?? TimeSpan.FromDays(1));
            _hash = SHA1.Create();
            _hash.Initialize();
        }

        /// <summary>Tries to read the current message from the queue without removing the message from the queue.</summary>
        /// <remarks>Within a transaction you cannot peek a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="F:System.TimeSpan.Zero" /> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="T:BusterWood.Msmq.QueueTransaction" />, <see cref="F:BusterWood.Msmq.QueueTransaction.Single" />, or <see cref="F:BusterWood.Msmq.QueueTransaction.Dtc" />.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message Peek(Properties properties = Properties.All, TimeSpan? timeout = default(TimeSpan?), QueueTransaction transaction = null)
        {
            return _queueReader.Peek(properties, timeout, transaction);
        }

        /// <summary>Tries to peek the current a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="F:System.TimeSpan.Zero" /> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public Task<Message> PeekAsync(Properties properties, TimeSpan? timeout = default(TimeSpan?))
        {
            return _queueReader.PeekAsync(properties, timeout);
        }

        /// <summary>Tries to receive a message from the queue</summary>
        /// <remarks>Within a transaction you cannot receive a message that you moved to a subqueue</remarks>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="F:System.TimeSpan.Zero" /> to return without waiting</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="T:BusterWood.Msmq.QueueTransaction" />, <see cref="F:BusterWood.Msmq.QueueTransaction.Single" />, or <see cref="F:BusterWood.Msmq.QueueTransaction.Dtc" />.</param>
        /// <returns>The message, or NULL if the receive times out</returns>
        public Message Read(Properties properties = Properties.All, TimeSpan? timeout = default(TimeSpan?), QueueTransaction transaction = null)
        {
            for (;;)
            {
                var msg = _queueReader.Read(properties | Properties.Label, timeout);
                var hash = ComputeHash(msg);
                var existingHash = _hashByLabel[msg.Label];
                if (hash == existingHash)
                {
                    Console.Error.WriteLine($"Dropping duplicate message for label '{msg.Label}'");
                    continue;
                }
                _hashByLabel[msg.Label] = hash;
                return msg;
            }
        }

        /// <summary>Tries to receive a message from the queue, which may complete synchronously or asynchronously if no message is ready</summary>
        /// <param name="properties">The properties to read</param>
        /// <param name="timeout">The time allowed, defaults to infinite.  Use <see cref="F:System.TimeSpan.Zero" /> to return without waiting</param>
        /// <returns>The a task that contains a message, or a task will a null Result if the receive times out</returns>
        public async Task<Message> ReadAsync(Properties properties, TimeSpan? timeout = default(TimeSpan?))
        {
            for (;;)
            {
                var msg = await _queueReader.ReadAsync(properties | Properties.Label, timeout);
                var hash = ComputeHash(msg);
                var existingHash = _hashByLabel[msg.Label];
                if (hash == existingHash)
                {
                    Console.Error.WriteLine($"Dropping duplicate message for label '{msg.Label}'");
                    continue;
                }
                _hashByLabel[msg.Label] = hash;
                return msg;
            }
        }

        /// <summary>Compute the hash for a message, defaults to computing the SHA1 of the message body</summary>
        protected virtual string ComputeHash(Message msg) => BitConverter.ToString(_hash.ComputeHash(msg.Body));

        public virtual void Dispose()
        {
            _hash.Dispose();
        }
    }
}
