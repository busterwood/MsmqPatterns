using System;

namespace BusterWood.Msmq
{
    /// <summary>
    /// The type of acknowledgements to be sent to the <see cref="Message.AdministrationQueue"/>
    /// </summary>
    [Flags]
    public enum AcknowledgmentTypes
    {
        None = 0,
        /// <summary>the message reached the destination queue</summary>
        PositiveArrival = 1,
        /// <summary>the message was received within the timeout</summary>
        PositiveReceive = 2,
        /// <summary>the message did NOT reach the destination queue</summary>
        NegativeArrival = 4,
        /// <summary>the message was not received within the timeout</summary>
        NegativeReceive = 8,

        NotAcknowledgeReceive = NegativeReceive | NegativeArrival,

        FullReachQueue = NegativeArrival | PositiveArrival,

        FullReceive = PositiveReceive | NegativeReceive,
    }
}