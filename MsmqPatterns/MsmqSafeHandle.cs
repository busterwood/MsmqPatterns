using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
namespace MsmqPatterns
{
    class MsmqSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        public static extern int MQCloseQueue(IntPtr hQueue);
        public MsmqSafeHandle(): base (true)
        {
        }

        public override bool IsInvalid => base.IsInvalid || IsClosed;
        protected override bool ReleaseHandle()
        {
            if (MQCloseQueue(handle) != 0)
                return false;
            return true;
        }
    }
}