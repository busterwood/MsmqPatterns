using System;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    public interface IQueueReader
    {
        Message Peek(Properties properties = Properties.All, TimeSpan? timeout = default(TimeSpan?), QueueTransaction transaction = null);
        Task<Message> PeekAsync(Properties properties, TimeSpan? timeout = default(TimeSpan?));
        Message Read(Properties properties = Properties.All, TimeSpan? timeout = default(TimeSpan?), QueueTransaction transaction = null);
        Task<Message> ReadAsync(Properties properties, TimeSpan? timeout = default(TimeSpan?));
    }
}