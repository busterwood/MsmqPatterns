using System;
using System.Diagnostics.Contracts;
using System.Text;

namespace BusterWood.Msmq
{
    /// <summary>Represents a message queue</summary>
    public abstract class Queue : IDisposable
    {
        internal QueueHandle _handle;

        /// <summary>Gets the full format name of this queue</summary>
        public string FormatName { get; private set; }

        /// <summary>How this queue was opened</summary>
        public QueueAccessMode AccessMode { get; }

        /// <summary>How the queue is shared</summary>
        public QueueShareReceive ShareMode { get; }

        /// <summary>Has the queue been closed? (or disposed)</summary>
        public bool IsClosed => _handle == null || _handle.IsClosed;

        internal Queue(string formatName, QueueAccessMode accessMode, QueueShareReceive shareMode)
        {
            Contract.Requires(formatName != null);
            AccessMode = accessMode;
            ShareMode = shareMode;
            FormatName = formatName;
        }

        /// <summary>Opens the queue</summary>
        internal void Open()
        {
            if (!IsClosed)
                _handle.Close();

            int res = Native.OpenQueue(FormatName, AccessMode, ShareMode, out _handle);
            if (res != 0)
                throw new QueueException(res);

            FormatName = FormatNameFromHandle(); // gets the full DNS format name
        }

        /// <summary>Closes this queue</summary>
        public virtual void Dispose()
        {
            if (IsClosed) return;
            _handle.Dispose();
        }

        string FormatNameFromHandle()
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(Queue));

            int size = 255;
            var sb = new StringBuilder(size);

            int res = Native.HandleToFormatName(_handle, sb, ref size);
            if (res != 0)
                throw new QueueException(res);

            sb.Length = size - 1; // remove null terminator
            return sb.ToString();
        }

        public override string ToString() => FormatName;

    }
}
