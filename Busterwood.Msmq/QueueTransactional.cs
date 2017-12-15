namespace BusterWood.Msmq
{
    /// <summary>Create a transactional queue or not?</summary>
    public enum QueueTransactional
    {
        /// <summary>Create a non-transactional queue</summary>
        None = 0,

        /// <summary>Create a transactional queue</summary>
        Transactional = 1,
    }
}