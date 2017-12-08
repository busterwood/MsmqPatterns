namespace BusterWood.Msmq
{
    /// <summary>
    /// The action to perform when calling <see cref="Queue.ReceiveByLookupId(Properties, long, LookupAction, System.TimeSpan?, Transaction)"/>
    /// </summary>
    public enum LookupAction
    {
        /// <summary>Peek the message with the requested <see cref="Message.LookupId"/></summary>
        PeekCurrent = 0x40000010,

        /// <summary>Peek the message after <see cref="Message.LookupId"/></summary>
        PeekNext = 0x40000011,

        /// <summary>Peek the message before <see cref="Message.LookupId"/></summary>
        PeekPrev = 0x40000012,

        /// <summary>Peek the first message in the queue. <see cref="Message.LookupId"/> must be set to zero</summary>
        PeekFirst = 0x40000014,

        /// <summary>Peek the last message in the queue. <see cref="Message.LookupId"/> must be set to zero</summary>
        PeekLast = 0x40000018,

        /// <summary>Receives the message with the requested <see cref="Message.LookupId"/></summary>
        ReceiveCurrent = 0x40000020,

        /// <summary>Receives the message after <see cref="Message.LookupId"/></summary>
        ReceiveNext = 0x40000021,

        /// <summary>Receives the message before <see cref="Message.LookupId"/></summary>
        ReceivePrev = 0x40000022,
        
        /// <summary>Receives the first message in the queue. <see cref="Message.LookupId"/> must be set to zero</summary>
        ReceiveFirst = 0x40000024,

        /// <summary>Receives the last message in the queue. <see cref="Message.LookupId"/> must be set to zero</summary>
        ReceiveLast = 0x40000028,
    }
}