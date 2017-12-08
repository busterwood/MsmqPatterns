namespace BusterWood.Msmq
{
    public enum MessageClass
    {
        Normal = 0x00,
        Report = 0x01,
        AccessDenied = (1 << 15) | 0x04,
        BadDestinationQueue = (1 << 15),
        BadEncryption = (1 << 15) | 0x07,
        BadSignature = (1 << 15) | 0x06,
        CouldNotEncrypt = (1 << 15) | 0x08,
        HopCountExceeded = (1 << 15) | 0x05,
        NotTransactionalQueue = (1 << 15) | 0x09,
        NotTransactionalMessage = (1 << 15) | 0x0a,
        Purged = (1 << 15) | 0x01,
        QueueDeleted = (1 << 15) | (1 << 14),
        QueueExceed_quota = (1 << 15) | 0x03,
        QueuePurged = (1 << 15) | (1 << 14) | 0x01,
        ReachQueue = 0x02,
        ReachQueueTimeout = (1 << 15) | 0x02,
        Receive = (1 << 14),
        ReceiveTimeout = (1 << 15) | (1 << 14) | 0x02,
    }
}