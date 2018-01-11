using Microsoft.Win32;
using System;
using System.Diagnostics.Contracts;
using System.Threading;

namespace BusterWood.Msmq
{

    /// <summary>Class that represents a message queue that you can post messages to</summary>
    public class QueueWriter : Queue
    {
        static int multicastCheck;

        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="Queues.PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
        public QueueWriter(string formatName) 
            : base(formatName, QueueAccessMode.Send, QueueShareReceive.Shared)
        {
            Open();
            CheckMulticastSendRate();
        }

        /// <summary>Report when multicast is rated limited</summary>
        private void CheckMulticastSendRate()
        {
            if (!FormatName.StartsWith("multicast=", StringComparison.OrdinalIgnoreCase) || Interlocked.CompareExchange(ref multicastCheck, 1, 0) != 0)
            {
                return;
            }

            const string kbpsKey = "SOFTWARE\\Microsoft\\MSMQ\\Parameters";
            var key = Registry.LocalMachine.OpenSubKey(kbpsKey);
            if (key == null)
                return; // MSMQ not installed?

            const string valueName = "MulticastRateKbitsPerSec";
            var val = key.GetValue(valueName);
            float mbps = (int)(val ?? 650) / 1000f;
            Console.Error.WriteLine($"Sending MSMQ multicast messages is limited to {mbps:N1}MB/sec via the registry HKLM\\{kbpsKey}\\{valueName}");
        }

        /// <summary>
        /// Asks MSMQ to attempt to deliver a message.
        /// To ensure the message reached the queue you need to check acknowledgement messages sent to the <see cref="Message.AdministrationQueue"/>
        /// </summary>
        /// <param name="message">The message to try to send</param>
        /// <param name="transaction">can be NULL for no transaction, a <see cref="QueueTransaction"/>, <see cref="QueueTransaction.Single"/>, or <see cref="QueueTransaction.Dtc"/>.</param>
        public void Write(Message message, QueueTransaction transaction = null)
        {
            Contract.Requires(message != null);

            message.Props.PrepareToSend();
            var props = message.Props.Allocate();
            try
            {
                int res;
                IntPtr txnHandle;
                if (transaction.TryGetHandle(out txnHandle))
                    res = Native.SendMessage(_handle, props, txnHandle);
                else
                    res = Native.SendMessage(_handle, props, transaction.InternalTransaction);

                if (Native.IsError(res))
                    throw new QueueException(res);
            }
            finally
            {
                message.Props.Free();
            }
        }
    }
}
