using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
namespace BusterWood.Msmq.Examples
{
    /// <summary>
    /// Reader for a transactional queue whose messages have been routed into "high" and "low" subqueues by <see cref="PriorityTransactionalRouter"/>
    /// </summary>
    public class PriorityTransactionalReader : IDisposable
    {
        readonly SubQueue _low;
        readonly SubQueue _high;

        public string InputQueueFormatName { get; }

        public PriorityTransactionalReader(string inputQueueFormatName)
        {
            Contract.Requires(inputQueueFormatName != null);
            InputQueueFormatName = inputQueueFormatName;
            _low = new SubQueue(InputQueueFormatName + ";low");
            _high = new SubQueue(InputQueueFormatName + ";high");
        }

        /// <summary>Read without waiting, returns null if no message is available in high or low priority subqueues</summary>
        public Message Read(Properties properties, QueueTransaction transaction)
        {
            Contract.Requires(transaction != null);
            return _high.Read(properties, TimeSpan.Zero, transaction) ?? _low.Read(properties, TimeSpan.Zero, transaction);
        }

        public async Task<Message> PeekAsync(Properties properties, QueueTransaction transaction)
        {
            Contract.Requires(transaction != null);
            var ht = _high.PeekAsync(properties);
            if (ht.IsCompleted || ht.IsFaulted) // always give high priority queue the first chance, it may complete synchronously
                return await ht;
            var lt = _low.PeekAsync(properties);
            Message[] results = await Task.WhenAll(ht, lt);
            return results.FirstOrDefault(m => m != null);
        }

        public Message Lookup(Properties properties, long lookupId, QueueTransaction transaction, LookupAction action = LookupAction.ReceiveCurrent, TimeSpan? timeout = null)
        {
            Contract.Requires(transaction != null);
            return _high.Lookup(properties, lookupId, action, timeout, transaction) ?? _low.Lookup(properties, lookupId, action, timeout, transaction);
        }

        public void Dispose()
        {
            _high.Dispose();
            _low.Dispose();
        }
    }
}