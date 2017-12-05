using System;
using System.ComponentModel;
using System.Messaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;
using BusterWood.Caching;

namespace MsmqPatterns
{
    public static class MessageQueueExtensions
    {
        const int MQ_SINGLE_MESSAGE = 3;
        const int MQ_MOVE_MESSAGE = 4; // System.Messaging does not support moving, we have to open the queue ourselves

        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        internal static extern int MQMoveMessage(IntPtr sourceQueue, SafeHandle targetQueue, long lookupId, IntPtr pTransaction);

        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        static extern int MQOpenQueue(string formatName, int access, int shareMode, out MsmqSafeHandle hQueue);

        static readonly Cache<string, SafeHandle> _cachedMoveHandles;

        static MessageQueueExtensions()
        {
            _cachedMoveHandles = new Cache<string, SafeHandle>(500, TimeSpan.FromMinutes(5));
            _cachedMoveHandles.Evicted += cachedMoveHandles_Evicted;
        }

        private static void cachedMoveHandles_Evicted(object sender, System.Collections.Generic.IReadOnlyDictionary<string, SafeHandle> evicted)
        {
            foreach (var handle in evicted.Values)
            {
                handle.Dispose();
            }
        }

        /// <summary>Move a message from the <paramref name="queue"/> to a subqueue</summary>
        /// <exception cref="Win32Exception">Thrown when the move fails</exception>
        /// <remarks>Fails with 0x80070006 Invalid handle when trying to move a message on a remote queue</remarks>
        public static void MoveMessage(this MessageQueue queue, string subqueueName, long lookupId, bool? transactional = null)
        {
            Contract.Requires(queue != null);
            Contract.Requires(subqueueName != null);

            var txn = transactional ?? queue.Transactional ? (IntPtr)MQ_SINGLE_MESSAGE : IntPtr.Zero;

            var subQFormatName = queue.FormatName + ";" + subqueueName;
            SafeHandle handle = GetSubQueueHandle(subQFormatName); // don't dispose as these are cached
            try
            {
                int result = MQMoveMessage(queue.ReadHandle, handle, lookupId, txn);
                if (result != 0)
                    throw new Win32Exception(result);
            }
            finally
            {
                _cachedMoveHandles[subQFormatName] = handle;
            }
        }

        private static SafeHandle GetSubQueueHandle(string formatName)
        {
            SafeHandle handle;
            lock (_cachedMoveHandles.SyncRoot)
            {
                handle = _cachedMoveHandles[formatName];
                if (handle != null)
                    _cachedMoveHandles.Remove(formatName); // take it out of the cache
            }
            return handle ?? OpenQueue(formatName, MQ_MOVE_MESSAGE, 0);
        }

        static SafeHandle OpenQueue(string formatName, int access, int shareMode)
        {
            MsmqSafeHandle handle;
            int result = MQOpenQueue(formatName, access, shareMode, out handle);
            if (result != 0)
                throw new Win32Exception(result);
            return handle;
        }

        /// <summary>
        /// Send a <paramref name="message"/> and wait for acknowledgement of delivery to destination queue via the <paramref name="adminQueue"/>
        /// </summary>
        /// <remarks>
        /// When sending a message to a remote queue you don't know if the message has reached the destination queue until we get an acknowledgement.
        /// When sending without an admin queue you might observe that messages sometimes disappear.
        /// </remarks>
        public static async Task SendAsync(this MessageQueue queue, MessageQueue adminQueue, Message message)
        {
            Contract.Requires(queue != null);
            Contract.Requires(adminQueue != null);
            Contract.Requires(message != null);

            message.AcknowledgeType |= AcknowledgeTypes.FullReachQueue;
            message.AdministrationQueue = adminQueue;

            queue.Send(message);

            await adminQueue.WaitForDelivery(message.Id);
        }        
        
        /// <summary>
        /// Send a <paramref name="message"/> and wait for acknowledgement of delivery to destination queue via the <paramref name="adminQueue"/>
        /// </summary>
        /// <remarks>
        /// When sending a message to a remote queue you don't know if the message has reached the destination queue until we get an acknowledgement.
        /// When sending without an admin queue you might observe that messages sometimes disappear.
        /// </remarks>
        public static async Task SendAsync(this MessageQueue queue, MessageQueue adminQueue, Message message, MessageQueueTransaction txn)
        {
            Contract.Requires(queue != null);
            Contract.Requires(adminQueue != null);
            Contract.Requires(message != null);
            Contract.Requires(txn != null);

            message.AcknowledgeType |= AcknowledgeTypes.FullReachQueue;
            message.AdministrationQueue = adminQueue;

            queue.Send(message, txn);

            await adminQueue.WaitForDelivery(message.Id);
        }

        /// <summary>wait for acknowledgement of message delivery to the destination queue</summary>
        public static Acknowledgment ReceiveAcknowledgement(this MessageQueue adminQueue, string correlationId)
        {
            adminQueue.MessageReadPropertyFilter.Acknowledgment = true;
            using (Message ack = adminQueue.ReceiveByCorrelationId(correlationId, MessageQueue.InfiniteTimeout))
            {
                return ack.Acknowledgment;
            }
        }

        /// <summary>wait for acknowledgement of message delivery to the destination queue</summary>
        public static async Task<Acknowledgment> ReceiveAcknowledgementAsync(this MessageQueue adminQueue, string correlationId)
        {
            adminQueue.MessageReadPropertyFilter.Acknowledgment = true;
            using (Message ack = await adminQueue.ReceiveByCorrelationIdAsync(correlationId))
            {
                return ack.Acknowledgment;
            }
        }

        /// <summary>wait for acknowledgement of message delivery to the destination queue</summary>
        public static async Task WaitForDelivery(this MessageQueue adminQueue, string correlationId)
        {
            adminQueue.MessageReadPropertyFilter.Acknowledgment = true;
            using (Message ack = await adminQueue.ReceiveByCorrelationIdAsync(correlationId))
            {
                switch (ack.Acknowledgment)
                {
                    case Acknowledgment.ReachQueueTimeout:
                        throw new TimeoutException();
                    case Acknowledgment.ReachQueue:
                    case Acknowledgment.Receive:
                        break;
                    default:
                        throw new AcknowledgmentException(ack.Acknowledgment);
                }
            }
        }

        /// <summary>wait for acknowledgement of message delivery to the destination queue</summary>
        public static async Task WaitForDelivery(this MessageQueue adminQueue, string correlationId, MessageQueueTransaction txn)
        {
            adminQueue.MessageReadPropertyFilter.Acknowledgment = true;
            using (Message ack = await adminQueue.ReceiveByCorrelationIdAsync(correlationId))
            {
                switch (ack.Acknowledgment)
                {
                    case Acknowledgment.ReachQueueTimeout:
                        throw new TimeoutException();
                    case Acknowledgment.ReachQueue:
                    case Acknowledgment.Receive:
                        break;
                    default:
                        throw new AcknowledgmentException(ack.Acknowledgment);
                }
            }
        }

        /// <summary>Async receive by <paramref name = "correlationId"/> for non-transactional queues</summary>
        public static async Task<Message> ReceiveByCorrelationIdAsync(this MessageQueue queue, string correlationId)
        {
            Contract.Requires(queue != null);
            Contract.Requires(correlationId != null);

            var currentFilter = queue.MessageReadPropertyFilter;
            queue.MessageReadPropertyFilter = (MessagePropertyFilter)currentFilter.Clone();
            queue.MessageReadPropertyFilter.CorrelationId = true;

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
        public static async Task<Message> TryPeekAsync(this MessageQueue queue, TimeSpan timeout)
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
        public static async Task<Message> TryPeekAsync(this MessageQueue queue, TimeSpan timeout, Cursor cursor, PeekAction action)
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
        public static Message TryRecieve(this MessageQueue queue, TimeSpan timeout)
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
        public static async Task<Message> TryRecieveAsync(this MessageQueue queue, TimeSpan timeout)
        {
            Contract.Requires(queue != null);
            try
            {
                return await Task.Factory.FromAsync(queue.BeginReceive(timeout), queue.EndReceive);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }

        /// <summary>Returns the next message from the <paramref name="queue"/>, or NULL if the <paramref name="timeout"/> is reached</summary>
        public static Message TryRecieve(this MessageQueue queue, TimeSpan timeout, MessageQueueTransaction txn)
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
        public static Message TryRecieve(this MessageQueue queue, TimeSpan timeout, MessageQueueTransactionType txnType)
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