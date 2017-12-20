namespace BusterWood.Msmq
{
    public enum SenderIdType
    {
        /// <summary>Do not attach sender id (faster)</summary>
        None = 0,

        /// <summary>Attach sender id (2.5 times slower)</summary>
        Sid = 1,
    }
}
