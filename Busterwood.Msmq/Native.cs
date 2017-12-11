using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace BusterWood.Msmq
{
    [ComVisible(false), SuppressUnmanagedCodeSecurity]
    partial class Native
    {
        const string MQRT = "mqrt.dll";

        public unsafe delegate void ReceiveCallback(int result, IntPtr handle, int timeout, int action, IntPtr propertiesPointer, NativeOverlapped* overlappedPointer, IntPtr cursorHandle);

        [DllImport(MQRT, EntryPoint = "MQOpenQueue", CharSet = CharSet.Unicode)]
        public static extern int OpenQueue(string formatName, QueueAccessMode access, QueueShareMode shareMode, out QueueHandle handle);

        [DllImport(MQRT, EntryPoint = "MQBeginTransaction")]
        public static extern int BeginTransaction(out ITransaction refTransaction);

        [DllImport(MQRT, EntryPoint = "MQCloseCursor")]
        public static extern int CloseCursor(IntPtr cursorHandle);

        [DllImport(MQRT, EntryPoint = "MQCloseQueue")]
        public static extern int CloseQueue(IntPtr handle);

        [DllImport(MQRT, EntryPoint = "MQDeleteQueue", CharSet = CharSet.Unicode)]
        public static extern int DeleteQueue(string formatName);

        [DllImport(MQRT, EntryPoint = "MQCreateQueue", CharSet = CharSet.Unicode)]
        public static extern int CreateQueue(IntPtr securityDescriptor, MQPROPS queueProperties, StringBuilder formatName, ref int formatNameLength);

        [DllImport(MQRT, EntryPoint = "MQHandleToFormatName", CharSet = CharSet.Unicode)]
        public static extern int HandleToFormatName(QueueHandle handle, StringBuilder formatName, ref int count);

        [DllImport(MQRT, EntryPoint = "MQGetQueueProperties", CharSet = CharSet.Unicode)]
        public static extern int GetQueueProperties(string formatName, MQPROPS queueProperties);

        [DllImport(MQRT, EntryPoint = "MQGetOverlappedResult", CharSet = CharSet.Unicode)]
        public unsafe static extern int GetOverlappedResult(NativeOverlapped* overlapped);

        [DllImport(MQRT, EntryPoint = "MQPathNameToFormatName", CharSet = CharSet.Unicode)]
        public static extern int PathNameToFormatName(string pathName, StringBuilder formatName, ref int count);

        [DllImport(MQRT, EntryPoint = "MQCreateCursor")]
        public static extern int CreateCursor(QueueHandle handle, out CursorHandle cursorHandle);

        [DllImport(MQRT, EntryPoint = "MQMarkMessageRejected")]
        public static extern int MarkMessageRejected(QueueHandle handle, long lookupId);

        [DllImport(MQRT, EntryPoint = "MQMoveMessage")]
        public static extern int MoveMessage(QueueHandle sourceQueue, QueueHandle targetQueue, long lookupId, IntPtr transaction);

        [DllImport(MQRT, EntryPoint = "MQMoveMessage")]
        public static extern int MoveMessage(QueueHandle sourceQueue, QueueHandle targetQueue, long lookupId, ITransaction transaction); //MSMQ internal transaction

        [DllImport(MQRT, EntryPoint = "MQPurgeQueue")]
        public static extern int PurgeQueue(QueueHandle sourceQueue);

        [DllImport(MQRT, EntryPoint = "MQReceiveMessage", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessage(
            QueueHandle handle, 
            uint timeout, 
            ReceiveAction action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            CursorHandle cursorHandle, 
            IntPtr transaction);

        [DllImport(MQRT, EntryPoint = "MQReceiveMessage", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessage(
            QueueHandle handle, 
            uint timeout,
            ReceiveAction action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            CursorHandle cursorHandle, 
            ITransaction transaction); //MSMQ internal transaction

        [DllImport(MQRT, EntryPoint = "MQReceiveMessageByLookupId", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessageByLookupId(
            QueueHandle handle, 
            long lookupId,
            LookupAction action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            IntPtr transaction);

        [DllImport(MQRT, EntryPoint = "MQReceiveMessageByLookupId", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessageByLookupId(
            QueueHandle handle, 
            long lookupId,
            LookupAction action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            ITransaction transaction); //MSMQ internal transaction

        [DllImport(MQRT, EntryPoint = "MQSendMessage", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(QueueHandle handle, MQPROPS properties, IntPtr transaction);

        [DllImport(MQRT, EntryPoint = "MQSendMessage", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(QueueHandle handle, MQPROPS properties, ITransaction transaction); //MSMQ internal transaction

        internal static bool NotEnoughMemory(int hresult)
        {
            switch ((ErrorCode)hresult)
            {
                case ErrorCode.BufferOverflow:
                case ErrorCode.LabelBufferTooSmall:
                case ErrorCode.ProviderNameBufferTooSmall:
                case ErrorCode.SenderCertificateBufferTooSmall:
                case ErrorCode.SenderIdBufferTooSmall:
                case ErrorCode.SecurityDescriptorBufferTooSmall:
                case ErrorCode.SignatureBufferTooSmall:
                case ErrorCode.SymmetricKeyBufferTooSmall:
                case ErrorCode.UserBufferTooSmall:
                case ErrorCode.FormatNameBufferTooSmall:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsError(int hresult)
        {
            bool isSuccessful = (hresult == 0x00000000);
            bool isInformation = ((hresult & unchecked((int)0xC0000000)) == 0x40000000);
            return (!isInformation && !isSuccessful);
        }

    }
}
