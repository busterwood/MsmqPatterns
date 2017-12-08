using System;
namespace BusterWood.Msmq
{
    /// <summary>How to open the <see cref="Queue"/></summary>
    [Flags]
    public enum QueueAccessMode
    {
        /// <summary>Messages can be retrieved from the queue, peeked at, or purged. Can be combined with <see cref="Admin"/></summary>
        Receive = 1,

        /// <summary>Messages can be sent to the queue. Can be combined with <see cref="Admin"/></summary>
        Send = 2,

        /// <summary>Can be used only when opening a subqueue, so you can call <see cref="Queue.Move(long, Queue, Transaction)"/></summary>
        Move = 4,

        /// <summary>Messages can be looked at but cannot be removed from the queue. Can be combined with <see cref="Admin"/></summary>
        Peek = 32,

        /// <summary>Allows access to local outgoing queues.  Can be combined with <see cref="Receive"/> or <see cref="Send"/></summary>
        Admin = 128,
    }
}