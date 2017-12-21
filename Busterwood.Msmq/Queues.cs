using System;
using System.Runtime.InteropServices;

namespace BusterWood.Msmq
{
    public static class Queues
    {
        /// <summary>Gets the path names of the private queues on a computer, the local computer is the default.</summary>
        public static string[] PrivateQueuePaths(string machine = null)
        {
            var props = new MessageProperties(6, Native.MANAGEMENT_BASE + 1);
            props.SetNull(Native.MANAGEMENT_PRIVATEQ);
            int status = Native.MgmtGetInfo(machine, "MACHINE", props.Allocate());
            props.Free();
            if (Native.IsError(status))
                throw new QueueException(status);

            IntPtr arrayPtr = props.GetStringVectorBasePointer(Native.MANAGEMENT_PRIVATEQ);
            uint numQueues = props.GetStringVectorLength(Native.MANAGEMENT_PRIVATEQ);
            var queues = new string[numQueues];
            for (int i = 0; i < numQueues; i++)
            {
                IntPtr pathPtr = Marshal.ReadIntPtr((IntPtr)((long)arrayPtr + i * IntPtr.Size));
                queues[i] = Marshal.PtrToStringUni(pathPtr);
                Native.FreeMemory(pathPtr);
            }

            Native.FreeMemory(arrayPtr);
            return queues;
        }
    }
}
