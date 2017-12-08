using Microsoft.Win32.SafeHandles;

namespace BusterWood.Msmq
{
    internal class CursorHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public static readonly CursorHandle None = new InvalidCursorHandle();

        protected CursorHandle(): base (true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Native.CloseCursor(handle);
            return true;
        }

        public override bool IsInvalid => base.IsInvalid || IsClosed;
        // Prevent exception when MQRT.DLL is not installed
        sealed class InvalidCursorHandle : CursorHandle
        {
            protected override bool ReleaseHandle() => true;
        }
    }
}