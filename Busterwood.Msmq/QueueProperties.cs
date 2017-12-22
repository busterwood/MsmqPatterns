namespace BusterWood.Msmq
{
    internal class QueueProperties : MessageProperties
    {
        public QueueProperties(): base (26, Native.QUEUE_PROPID_BASE + 1)
        {
        }
    }
}