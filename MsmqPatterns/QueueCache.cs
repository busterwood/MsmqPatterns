﻿using System;
using BusterWood.Caching;
using System.Collections.Generic;
using BusterWood.Msmq;
using System.Diagnostics.Contracts;

namespace BusterWood.MsmqPatterns
{
    public class QueueCache<T> where T: Queue
    {
        readonly Cache<Key, T> _cache;

        public QueueCache(Func<string, QueueAccessMode, QueueShareReceive, T> factory) : this(factory, 500, TimeSpan.FromMinutes(5))
        {
        }

        readonly Func<string, QueueAccessMode, QueueShareReceive, T> _factory;

        public QueueCache(Func<string, QueueAccessMode, QueueShareReceive, T> factory, int? gen0Limit, TimeSpan? timeToLive)
        {
            Contract.Requires(factory != null);
            _factory = factory;
            _cache = new Cache<Key, T>(gen0Limit, timeToLive);
            _cache.Evicted += cachedMoveHandles_Evicted;
        }

        private void cachedMoveHandles_Evicted(object sender, IReadOnlyDictionary<Key, T> evicted)
        {
            foreach (var q in evicted.Values)
            {
                q.Dispose();
            }
        }

        public T Open(string formatName, QueueAccessMode mode, QueueShareReceive share = QueueShareReceive.Shared)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(formatName));
            var key = new Key(formatName, mode, share);

            lock (_cache.SyncRoot)
            {
                T queue = _cache[key];
                if (queue == null || queue.IsClosed)
                    _cache[key] = queue = _factory(formatName, mode, share);
                return queue;
            }
        }

        public T Borrow(string formatName, QueueAccessMode mode, QueueShareReceive share = QueueShareReceive.Shared)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(formatName));
            T queue;
            var key = new Key(formatName, mode, share);
            lock (_cache.SyncRoot)
            {
                queue = _cache[key];
                if (queue != null)
                    _cache.Remove(key); // take it out of the cache
            }

            return queue == null || queue.IsClosed ? _factory(formatName, mode, share) : queue;
        }

        public void Return(T queue)
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
            public readonly QueueShareReceive share;

            public Key(string formatName, QueueAccessMode mode, QueueShareReceive share)
            {
                this.formatName = formatName;
                this.mode = mode;
                this.share = share;
            }
        }
    }
}