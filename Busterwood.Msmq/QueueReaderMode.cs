namespace BusterWood.Msmq
{
    public enum QueueReaderMode
    {
        /// <summary>Messages can be read from the queue, which removes the message from the queue</summary>
        Receive = 1,

        /// <summary>Messages can be only be peek at (viewed) without the message being removed from the queue</summary>
        Peek = 32,
    }
}
