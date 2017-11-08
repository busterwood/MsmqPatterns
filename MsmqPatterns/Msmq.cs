using System.ComponentModel;
using System.Runtime.InteropServices;
namespace MsmqPatterns
{
    static class Msmq
    {
        [DllImport("mqrt.dll", CharSet = CharSet.Unicode)]
        static extern int MQOpenQueue(string formatName, int access, int shareMode, out MsmqSafeHandle hQueue);
        public static SafeHandle OpenQueue(string formatName, int access, int shareMode)
        {
            MsmqSafeHandle handle;
            int result = MQOpenQueue(formatName, access, shareMode, out handle);
            if (result != 0)
                throw new Win32Exception(result);
            return handle;
        }
    }
}