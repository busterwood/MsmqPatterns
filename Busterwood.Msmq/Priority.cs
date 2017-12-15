namespace BusterWood.Msmq
{
    /// <summary>
    /// The priority of a message on a non-transactional queue.
    /// Messages on transaction queues always have priority <see cref="Lowest"/>
    /// </summary>
    public enum Priority
    {
        Lowest = 0,
        VeryLow = 1,
        Low = 2,
        Normal = 3,
        AboveNormal = 4,
        High = 5,
        VeryHigh = 6,
        Highest = 7,
    }
}