using System;
using System.Runtime.InteropServices;

namespace BusterWood.Msmq
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct MQPROPVARIANTS
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct CAUB
        {
            internal uint cElems;
            internal IntPtr pElems;
        }

        [FieldOffset(0)]
        internal short vt;
        [FieldOffset(2)]
        internal short wReserved1;
        [FieldOffset(4)]
        internal short wReserved2;
        [FieldOffset(6)]
        internal short wReserved3;
        [FieldOffset(8)]
        internal byte bVal;
        [FieldOffset(8)]
        internal short iVal;
        [FieldOffset(8)]
        internal int lVal;
        [FieldOffset(8)]
        internal long hVal;
        [FieldOffset(8)]
        internal IntPtr ptr;
        [FieldOffset(8)]
        internal CAUB caub;
    }
}