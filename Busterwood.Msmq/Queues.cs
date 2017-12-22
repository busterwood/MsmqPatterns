using System;
using System.Diagnostics;
using System.Linq;
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

        public static void DeleteOldTempQueues()
        {
            var qis = PrivateQueuePaths()
                .Where(p => p.Contains("\\private$\\temp."))
                .Select(p => GetInfo(Queue.PathToFormatName(p)))
                .ToList();

            var procbyId = Process.GetProcesses().ToDictionary(p => p.Id);

            foreach (var q in qis)
            {
                var bits = q.Label.Split(':');
                int id;
                if (bits.Length == 2 && int.TryParse(bits[1], out id) && (!procbyId.ContainsKey(id) || !bits[0].Equals(procbyId[id].ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    Queue.TryDelete(q.FormatName);
                    Console.Error.WriteLine("INFO deleted old temporary queue " + q.FormatName);
                }
            }
        }

        private static QueueInformation GetInfo(string formatName)
        {
            var props = new QueueProperties();
            //props.SetNull(Native.QUEUE_PROPID_CREATE_TIME);
            props.SetNull(Native.QUEUE_PROPID_LABEL);

            int status = Native.GetQueueProperties(formatName, props.Allocate());
            props.Free();
            if (Native.IsError(status))
                throw new QueueException(status);

            string label = "";
            IntPtr handle = props.GetIntPtr(Native.QUEUE_PROPID_LABEL);
            if (handle != IntPtr.Zero)
            {
                label = Marshal.PtrToStringUni(handle);
                Native.FreeMemory(handle);
            }

            return new QueueInformation { FormatName = formatName, Label = label };
        }
    }
    public class QueueInformation
    {
        public string FormatName { get; internal set; }
        
        //public string Path { get;  }

        public string Label { get; internal set; }

        //public Guid Class { get; set; }

        public override string ToString() => FormatName;
    }

}
