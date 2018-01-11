using System.Diagnostics.Contracts;

namespace BusterWood.Msmq
{

    /// <summary>A sub-queue that you can peek and read from, but also move message to via <see cref="Queue.MoveMessage(QueueReader, SubQueue, long, QueueTransaction)"/></summary>
    public class SubQueue : QueueReader
    {
        private QueueHandle _moveHandle;

        internal QueueHandle MoveHandle => _moveHandle;

        /// <summary>Opens a queue using a <paramref name="formatName"/>.  Use <see cref="Queues.PathToFormatName(string)"/> to get the <paramref name="formatName"/> for a queue path.</summary>
        public SubQueue(string formatName, QueueReaderMode mode = QueueReaderMode.Receive, QueueShareReceive share = QueueShareReceive.Shared)
            : base(formatName, mode, share)
        {
            Contract.Requires(formatName.IndexOf(';') > 0, "formatName is not a subqueue");

            int res = Native.OpenQueue(FormatName, QueueAccessMode.Move, QueueShareReceive.Shared, out _moveHandle);
            if (res != 0)
                throw new QueueException(res);
        }

        public override void Dispose()
        {
            base.Dispose();
            _moveHandle?.Dispose();
        }
    }
}
