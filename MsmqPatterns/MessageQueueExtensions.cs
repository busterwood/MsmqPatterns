using System;
using System.ComponentModel;
using System.Messaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace MsmqPatterns
{
    public static class MessageQueueExtensions
    {
        const int MQ_SINGLE_MESSAGE = 3;
        const int MQ_MOVE_MESSAGE = 4; // System.Messaging does not support moving, we have to open the queue ourselves

        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        internal static extern int MQMoveMessage(IntPtr sourceQueue, SafeHandle targetQueue, long lookupId, IntPtr pTransaction);

        /// <summary>Move a message from the <paramref name="queue"/> to a subqueue</summary>
        /// <exception cref="Win32Exception">Thrown when the move fails</exception>
        public static void MoveMessage(this MessageQueue queue, string subqueueName, long lookupId, bool? transactional = null)
        {
            Contract.Requires(queue != null);
            Contract.Requires(subqueueName != null);

            var txn = transactional ?? queue.Transactional ? (IntPtr)MQ_SINGLE_MESSAGE : IntPtr.Zero;
            
            //TODO: add cache of subqueues as opening the queue is quite slow
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
            Contract.Requires(queue != null);
            Contract.Requires(correlationId != null);

            var currentFilter = queue.MessageReadPropertyFilter;
            queue.MessageReadPropertyFilter = new MessagePropertyFilter{CorrelationId = true};
            using (var cursor = queue.CreateCursor())
            {
                var action = PeekAction.Current;
                for (;;)
                {
                    using (var peeked = await Task.Factory.FromAsync(queue.BeginPeek(MessageQueue.InfiniteTimeout, cursor, action, null, null), queue.EndPeek))
                    {
                        if (peeked.CorrelationId == correlationId)
                        {
                            queue.MessageReadPropertyFilter = currentFilter;
                            return queue.Receive(MessageQueue.InfiniteTimeout, cursor);
                        }
                    }
                    action = PeekAction.Next;
                }
            }
        }

        /// <summary>Returns a message from a cursor, or NULL if the <paramref name="timeout"/> is reached</summary>
        public static async Task<Message> PeekAsync(this MessageQueue queue, TimeSpan timeout)
        {
            Contract.Requires(queue != null);
            try
            {
                return await Task.Factory.FromAsync(queue.BeginPeek(timeout), queue.EndPeek);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }
        
        /// <summary>Returns a message from a cursor, or NULL if the <paramref name="timeout"/> is reached</summary>
        public static async Task<Message> PeekAsync(this MessageQueue queue, TimeSpan timeout, Cursor cursor, PeekAction action)
        {
            Contract.Requires(queue != null);
            Contract.Requires(cursor != null);
            try
            {
                return await Task.Factory.FromAsync(queue.BeginPeek(timeout, cursor, action, null, null), queue.EndPeek);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }

        /// <summary>Returns the next message from the <paramref name="queue"/>, or NULL if the <paramref name="timeout"/> is reached</summary>
        public static Message RecieveWithTimeout(this MessageQueue queue, TimeSpan timeout)
        {
            Contract.Requires(queue != null);
            try
            {
                return queue.Receive(timeout);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }

        /// <summary>Returns the next message from the <paramref name="queue"/>, or NULL if the <paramref name="timeout"/> is reached</summary>
        public static Message RecieveWithTimeout(this MessageQueue queue, TimeSpan timeout, MessageQueueTransaction txn)
        {
            Contract.Requires(queue != null);
            Contract.Requires(txn != null);
            try
            {
                return queue.Receive(timeout, txn);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }

        /// <summary>Returns the next message from the <paramref name="queue"/>, or NULL if the <paramref name="timeout"/> is reached</summary>
        public static Message RecieveWithTimeout(this MessageQueue queue, TimeSpan timeout, MessageQueueTransactionType txnType)
        {
            Contract.Requires(queue != null);
            try
            {
                return queue.Receive(timeout, txnType);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }

        public static Message SendRequest(this MessageQueue requestQueue, Message request, MessageQueue replyQueue, MessageQueue adminQueue)
        {
            var rr = new RequestReply(requestQueue, replyQueue, adminQueue);
            return rr.SendRequest(request);
        }

        public static Task<Message> SendRequestAsync(this MessageQueue requestQueue, Message request, MessageQueue replyQueue, MessageQueue adminQueue)
        {
            var rr = new RequestReply(requestQueue, replyQueue, adminQueue);
            return rr.SendRequestAsync(request);
        }
    }
}