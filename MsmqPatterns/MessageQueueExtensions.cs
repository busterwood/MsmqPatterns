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
        public const int MQ_SINGLE_MESSAGE = 3;

        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        public static extern int MQMoveMessage(IntPtr sourceQueue, SafeHandle targetQueue, long lookupId, IntPtr pTransaction);

        /// <summary>Move a message to a subqueue</summary>
        public static void MoveMessage(this MessageQueue queue, string subqueueName, long lookupId, bool? transactional = null)
        {
            var txn = transactional ?? queue.Transactional ? (IntPtr)MQ_SINGLE_MESSAGE : IntPtr.Zero;
            using (var handle = Msmq.OpenQueue(queue.FormatName + ";" + subqueueName, 4, 0))
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

    static class Msmq
    {
        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        static extern int MQOpenQueue(string formatName, int access, int shareMode, out MsmqSafeHandle hQueue);

        public static SafeHandle OpenQueue(string formatName, int access, int shareMode)
        {
            MsmqSafeHandle handle;
            int result = MQOpenQueue(formatName, access, shareMode, out handle);
            if (result != 0)
                throw new Win32Exception(result);
            return handle;
        }
    }

    class MsmqSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        public static extern int MQCloseQueue(IntPtr hQueue);

        public MsmqSafeHandle() : base(true)
        {
        }

        public override bool IsInvalid => base.IsInvalid || IsClosed;

        protected override bool ReleaseHandle()
        {
            if (MQCloseQueue(handle) != 0)
                return false;
            return true;
        }
    }
}