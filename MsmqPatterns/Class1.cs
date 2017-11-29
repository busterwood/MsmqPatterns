using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    class ReceiveAny
    {
        readonly MessageQueue[] _queues;
        readonly Task<Message>[] _tasks;
        readonly Queue<MessageWithQueue> _peeked;

        public ReceiveAny(IReadOnlyCollection<MessageQueue> queues)
        {
            Contract.Requires(queues != null);
            _queues = queues.ToArray();
            _tasks = new Task<Message>[_queues.Length];
        }

        public async Task<Message> Receive()
        {
            lock(_peeked)
            {
                if (_peeked.Count > 0)
                {
                    var mq = _peeked.Dequeue();
                    return mq.Message;
                }
            }

            await Task.WhenAny(_tasks);

            lock (_peeked)
            {
                if (_peeked.Count == 0)
                {
                    Monitor.Wait(_peeked);
                }
                var mq = _peeked.Dequeue();
                return mq.Message;

            }
        }

        struct MessageWithQueue
        {
            public MessageQueue Queue { get; }
            public Message Message { get; }

            public MessageWithQueue(Message message, MessageQueue queue)
            {
                Message = message;
                Queue = queue;
            }
        }

    }
}
