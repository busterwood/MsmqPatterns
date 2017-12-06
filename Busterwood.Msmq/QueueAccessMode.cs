using System;
namespace Busterwood.Msmq
{
    [Flags]
    public enum QueueAccessMode
    {
        Receive = 1,
        Send = 2,
        Move = 4,
        Peek = 32,
        Admin = 128,
    }
}