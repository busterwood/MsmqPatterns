using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace BusterWood.Msmq
{
    public static class Queues
    {
        /// <summary>Creates a path name for a temporary queue, based on the current process</summary>
        public static string NewTempQueuePath()
        {
            return $".\\private$\\temp.{Guid.NewGuid():D}";
        }

        /// <summary>Creates a message queue (if it does not already exist), returning the format name of the queue.</summary>
        /// <param name="path">the path (NOT format name) of the queue</param>
        /// <param name="transactional">create a transactional queue or not?</param>
        /// <param name="quotaKB">Maximum size of the queue, in KB, defaults to 20MB</param>
        /// <param name="label">the label to add to the queue</param>
        /// <param name="multicast">the multicast address to attach to the queue</param>
        public static string TryCreate(string path, QueueTransactional transactional, int quotaKB = 20000, string label = null, IPEndPoint multicast = null)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(path));
            Contract.Requires(label == null || label.Length < 125);
            Contract.Ensures(Contract.Result<string>() != null);

            const int MaxLabelLength = 124;

            //Create properties.
            var properties = new QueueProperties();
            properties.SetString(Native.QUEUE_PROPID_PATHNAME, path);
            properties.SetByte(Native.QUEUE_PROPID_TRANSACTION, (byte)transactional);
            properties.SetUInt(Native.QUEUE_PROPID_QUOTA, quotaKB);
            if (label != null)
                properties.SetString(Native.QUEUE_PROPID_LABEL, label);
            if (multicast != null)
                properties.SetString(Native.QUEUE_PROPID_MULTICAST_ADDRESS, $"{multicast.Address}:{multicast.Port}");

            var formatName = new StringBuilder(MaxLabelLength);
            int len = MaxLabelLength;

            //Try to create queue.
            int res = Native.CreateQueue(IntPtr.Zero, properties.Allocate(), formatName, ref len);
            properties.Free();

            if ((ErrorCode)res == ErrorCode.QueueExists)
                return PathToFormatName(path);

            if (Native.IsError(res))
                throw new QueueException(res);

            formatName.Length = len;
            return formatName.ToString();
        }

        /// <summary>Tries to delete an existing message queue, returns TRUE if the queue was deleted, FALSE if the queue does not exists</summary>
        /// <param name="formatName">The format name (NOT path name) of the queue</param>
        public static bool TryDelete(string formatName)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(formatName));

            int res = Native.DeleteQueue(formatName);

            if ((ErrorCode)res == ErrorCode.QueueNotFound)
                return false;

            if (Native.IsError(res))
                throw new QueueException(res);

            return true;
        }

        /// <summary>converts a queue path to a format name</summary>
        public static string PathToFormatName(string path)
        {
            int size = 255;
            var sb = new StringBuilder(size);
            int res = Native.PathNameToFormatName(path, sb, ref size);
            if (res != 0)
                throw new QueueException(res);
            sb.Length = size - 1;
            return sb.ToString();
        }

        /// <summary>Tests if a queue existing. Does NOT accept format names</summary>
        public static bool Exists(string path)
        {
            Contract.Requires(path != null);

            int size = 255;
            var sb = new StringBuilder(size);
            int res = Native.PathNameToFormatName(path, sb, ref size);
            if ((ErrorCode)res == ErrorCode.QueueNotFound)
                return false;

            if (res != 0)
                throw new QueueException(res);

            return true;
        }

        /// <summary>Returns the transactional property of the queue</summary>
        public static QueueTransactional IsTransactional(string formatName)
        {
            Contract.Requires(formatName != null);
            var props = new QueueProperties();
            props.SetByte(Native.QUEUE_PROPID_TRANSACTION, 0);
            int status = Native.GetQueueProperties(formatName, props.Allocate());
            props.Free();
            if (Native.IsError(status))
                throw new QueueException(status);

            return (QueueTransactional)props.GetByte(Native.QUEUE_PROPID_TRANSACTION);
        }

        /// <summary>Move the message specified by <paramref name="lookupId"/> from <paramref name="sourceQueue"/> to the <paramref name="targetQueue"/>.</summary>
        /// <remarks>
        /// Moving message is 10 to 100 times faster than sending the message to another queue.
        /// Within a transaction you cannot receive a message that you moved to a subqueue.
        /// </remarks>
        public static void MoveMessage(QueueReader sourceQueue, SubQueue targetQueue, long lookupId, QueueTransaction transaction = null)
        {
            Contract.Requires(sourceQueue != null);
            Contract.Requires(targetQueue != null);

            if (sourceQueue.IsClosed) throw new ObjectDisposedException(nameof(sourceQueue));
            if (targetQueue.IsClosed) throw new ObjectDisposedException(nameof(targetQueue));

            int res;
            IntPtr txnHandle;
            if (transaction.TryGetHandle(out txnHandle))
                res = Native.MoveMessage(sourceQueue._handle, targetQueue.MoveHandle, lookupId, txnHandle);
            else
                res = Native.MoveMessage(sourceQueue._handle, targetQueue.MoveHandle, lookupId, transaction.InternalTransaction);

            if (Native.IsError(res))
                throw new QueueException(res);
        }

        /// <summary>Gets the path names of the private queues on a computer, the local computer is the default.</summary>
        public static string[] GetPrivateQueuePaths(string machine = null)
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

        /// <summary>
        /// Tries to delete all temporary queues that don't have a running process
        /// </summary>
        public static void DeleteOldTempQueues()
        {
            var qis = GetPrivateQueuePaths()
                .Where(p => p.Contains("\\private$\\temp."))
                .Select(p => GetInfo(PathToFormatName(p)))
                .ToList();

            var procbyId = Process.GetProcesses().ToDictionary(p => p.Id);

            foreach (var q in qis)
            {
                var bits = q.Label.Split(':');

                int id;
                if (bits.Length != 2 || !int.TryParse(bits[1], out id))
                    continue;

                var procName = bits[0];
                if (!procbyId.ContainsKey(id) || !procName.Equals(procbyId[id].ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(q.FormatName);
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
