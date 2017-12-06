using System;
namespace Busterwood.Msmq
{
    [Flags]
    public enum Journal
    {
        None = 0,

        /// <summary>
        /// Send the message to the dead-letter queue if it cannot be delivered?
        /// </summary>
        DeadLetter = 1,

        /// <summary>
        /// Send a copy of the message to the Journal sub-queue?
        /// </summary>
        Journal = 2,
    }
}