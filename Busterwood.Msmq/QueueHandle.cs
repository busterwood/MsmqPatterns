using Microsoft.Win32.SafeHandles;

namespace BusterWood.Msmq
{
    class QueueHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public static readonly QueueHandle InvalidHandle = new InvalidMessageQueueHandle();

        QueueHandle(): base (true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Native.CloseQueue(handle);
            return true;
        }

        public override bool IsInvalid => base.IsInvalid || IsClosed;

        // Prevent exception when MQRT.DLL is not installed
        sealed class InvalidMessageQueueHandle : QueueHandle
        {
            protected override bool ReleaseHandle() => true;
        }
    }
}