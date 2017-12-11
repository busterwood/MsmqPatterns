using System;
using BusterWood.Caching;
using System.Collections.Generic;
using BusterWood.Msmq;
using System.Diagnostics.Contracts;

namespace MsmqPatterns
{
    public class QueueCache
    {
        readonly Cache<Key, Queue> _cache;

        public QueueCache() : this(500, TimeSpan.FromMinutes(5))
        {
        }

        public QueueCache(int? gen0Limit, TimeSpan? timeToLive)
        {
            _cache = new Cache<Key, Queue>(gen0Limit, timeToLive);
            _cache.Evicted += cachedMoveHandles_Evicted;
        }

        private void cachedMoveHandles_Evicted(object sender, IReadOnlyDictionary<Key, Queue> evicted)
        {
            foreach (var q in evicted.Values)
            {
                q.Dispose();
            }
        }

        public Queue Open(string formatName, QueueAccessMode mode, QueueShareMode share = QueueShareMode.Shared)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(formatName));
            var key = new Key(formatName, mode, share);

            lock (_cache.SyncRoot)
            {
                Queue queue = _cache[key];
                if (queue == null || queue.IsClosed)
                    _cache[key] = queue = Queue.Open(formatName, mode, share);
                return queue;
            }
        }

        public Queue Borrow(string formatName, QueueAccessMode mode, QueueShareMode share = QueueShareMode.Shared)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(formatName));
            Queue queue;
            var key = new Key(formatName, mode, share);
            lock (_cache.SyncRoot)
            {
                queue = _cache[key];
                if (queue != null)
                    _cache.Remove(key); // take it out of the cache
            }

            return queue == null || queue.IsClosed ? Queue.Open(formatName, mode, share) : queue;
        }

        public void Return(Queue queue)
        {
            Contract.Requires(queue != null);
            if (queue.IsClosed) return;
            var key = new Key(queue.FormatName, queue.AccessMode, queue.ShareMode);
            _cache[key] = queue;
        }

        struct Key
        {
            public readonly string formatName;
            public readonly QueueAccessMode mode;
            public readonly QueueShareMode share;

            public Key(string formatName, QueueAccessMode mode, QueueShareMode share)
            {
                this.formatName = formatName;
                this.mode = mode;
                this.share = share;
            }
        }
    }
}