namespace BusterWood.Msmq
{
    /// Queue actions
    public enum QueueAction
    {
        /// <summary>Reads the current message off the queue</summary>
        Receive = 0x00000000,

        /// <summary>Reads the current message but leaves the message on the queue</summary>
        PeekCurrent = unchecked ((int)0x80000000),

        /// <summary>Reads the next message (only applies to cursors)</summary>
        PeekNext = unchecked ((int)0x80000001),
    }
}