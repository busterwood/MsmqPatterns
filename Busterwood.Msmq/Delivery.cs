namespace BusterWood.Msmq
{
    /// <summary>The type of message delivery required</summary>
    public enum Delivery
    {
        /// <summary>Express messages are faster as they are only stored in memory, which means they will be lost if the MSMQ service is stopped.</summary>
        Express = 1,

        /// <summary>Recoverable messages are stored on disk so will NOT be lost if the MSMQ service is stopped.</summary>
        Recoverable = 2,
    }
}