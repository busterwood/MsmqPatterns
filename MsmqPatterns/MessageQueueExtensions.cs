using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Messaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    static class MessageQueueExtensions
    {
        const int MQ_SINGLE_MESSAGE = 3;
        const int MQ_MOVE_MESSAGE = 4; // System.Messaging does not support moving, we have to open the queue ourselves

        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        public static extern int MQMoveMessage(IntPtr sourceQueue, SafeHandle targetQueue, long lookupId, IntPtr pTransaction);

        /// <summary>Move a message to a subqueue</summary>
        public static void MoveMessage(this MessageQueue queue, string subqueueName, long lookupId, bool? transactional = null)
        {
            var txn = transactional ?? queue.Transactional ? (IntPtr)MQ_SINGLE_MESSAGE : IntPtr.Zero;
            using (var handle = Msmq.OpenQueue(queue.FormatName + ";" + subqueueName, MQ_MOVE_MESSAGE, 0))
            {
                int result = MQMoveMessage(queue.ReadHandle, handle, lookupId, txn);
                if (result != 0)
                    throw new Win32Exception(result);
            }
        }

        /// <summary>Async receive by <paramref name = "correlationId"/> for non-transactional queues</summary>
        public static async Task<Message> ReceiveByCorrelationIdAsync(this MessageQueue queue, string correlationId)
        {
            var currentFilter = queue.MessageReadPropertyFilter;
            queue.MessageReadPropertyFilter = new MessagePropertyFilter{CorrelationId = true};
            using (var cursor = queue.CreateCursor())
            {
                var action = PeekAction.Current;
                for (;;)
                {
                    using (var peeked = await Task.Factory.FromAsync(queue.BeginPeek(TimeSpan.MaxValue, cursor, action, null, null), queue.EndPeek))
                    {
                        if (peeked.CorrelationId == correlationId)
                        {
                            queue.MessageReadPropertyFilter = currentFilter;
                            return queue.Receive(TimeSpan.MaxValue, cursor);
                        }
                    }
                    action = PeekAction.Next;
                }
            }
        }
    }
}