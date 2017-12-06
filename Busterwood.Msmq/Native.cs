using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace Busterwood.Msmq
{
    [ComVisible(false), SuppressUnmanagedCodeSecurity]
    partial class Native
    {
        public unsafe delegate void ReceiveCallback(int result, IntPtr handle, int timeout, int action, IntPtr propertiesPointer, NativeOverlapped* overlappedPointer, IntPtr cursorHandle);

        [DllImport("mqrt.dll", EntryPoint = "MQOpenQueue", CharSet = CharSet.Unicode)]
        public static extern int OpenQueue(string formatName, QueueAccessMode access, QueueShareMode shareMode, out QueueHandle handle);

        [DllImport("mqrt.dll", EntryPoint = "MQBeginTransaction")]
        public static extern int BeginTransaction(out ITransaction refTransaction);

        [DllImport("mqrt.dll", EntryPoint = "MQCloseCursor")]
        public static extern int CloseCursor(IntPtr cursorHandle);

        [DllImport("mqrt.dll", EntryPoint = "MQCloseQueue")]
        public static extern int CloseQueue(IntPtr handle);

        [DllImport("mqrt.dll", EntryPoint = "MQHandleToFormatName", CharSet = CharSet.Unicode)]
        public static extern int HandleToFormatName(QueueHandle handle, StringBuilder formatName, ref int count);

        [DllImport("mqrt.dll", EntryPoint = "MQPathNameToFormatName", CharSet = CharSet.Unicode)]
        public static extern int PathNameToFormatName(string pathName, StringBuilder formatName, ref int count);

        [DllImport("mqrt.dll", EntryPoint = "MQCreateCursor")]
        public static extern int CreateCursor(QueueHandle handle, out CursorHandle cursorHandle);

        [DllImport("mqrt.dll", EntryPoint = "MQMoveMessage")]
        public static extern int MoveMessage(QueueHandle sourceQueue, QueueHandle targetQueue, long lookupId, IntPtr transaction);

        [DllImport("mqrt.dll", EntryPoint = "MQMoveMessage")]
        public static extern int MoveMessage(QueueHandle sourceQueue, QueueHandle targetQueue, long lookupId, ITransaction transaction); //MSMQ internal transaction

        [DllImport("mqrt.dll", EntryPoint = "MQReceiveMessage", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessage(
            QueueHandle handle, 
            uint timeout, 
            QueueAction action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            CursorHandle cursorHandle, 
            IntPtr transaction);

        [DllImport("mqrt.dll", EntryPoint = "MQReceiveMessage", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessage(
            QueueHandle handle, 
            uint timeout, 
            QueueAction action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            CursorHandle cursorHandle, 
            ITransaction transaction); //MSMQ internal transaction

        [DllImport("mqrt.dll", EntryPoint = "MQReceiveMessageByLookupId", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessageByLookupId(
            QueueHandle handle, 
            long lookupId, 
            int action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            IntPtr transaction);

        [DllImport("mqrt.dll", EntryPoint = "MQReceiveMessageByLookupId", CharSet = CharSet.Unicode)]
        public unsafe static extern int ReceiveMessageByLookupId(
            QueueHandle handle, 
            long lookupId, 
            int action, 
            MQPROPS properties, 
            NativeOverlapped* overlapped,
            ReceiveCallback receiveCallback, 
            ITransaction transaction); //MSMQ internal transaction

        [DllImport("mqrt.dll", EntryPoint = "MQSendMessage", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(QueueHandle handle, MQPROPS properties, IntPtr transaction);

        [DllImport("mqrt.dll", EntryPoint = "MQSendMessage", CharSet = CharSet.Unicode)]
        public static extern int SendMessage(QueueHandle handle, MQPROPS properties, ITransaction transaction); //MSMQ internal transaction
    }
}
