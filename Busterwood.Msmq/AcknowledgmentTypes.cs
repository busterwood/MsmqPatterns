namespace BusterWood.Msmq
{
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
        ReachQueue = NegativeArrival | PositiveArrival,
    }
}