namespace BusterWood.Msmq
{
    public enum LookupAction
    {
        PeekCurrent = 0x40000010,
        PeekNext = 0x40000011,
        PeekPrev = 0x40000012,
        PeekFirst = 0x40000014,
        PeekLast = 0x40000018,
        ReceiveCurrent = 0x40000020,
        ReceiveNext = 0x40000021,
        ReceivePrev = 0x40000022,
        ReceiveFirst = 0x40000024,
        ReceiveLast = 0x40000028,
    }
}