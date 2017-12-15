namespace BusterWood.Msmq
{
    /// <summary>Can one or many processes read from the queue?</summary>
    public enum QueueShareReceive
    {
        /// <summary>Any process can read messages fro the queue</summary>
        Shared = 0,

        /// <summary>Only this <see cref="QueueReader"/> can read messages from the queue, no other process can</summary>
        ExclusiveReceive = 1,
    }
}