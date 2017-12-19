using System;
using System.Threading.Tasks;
namespace BusterWood.Msmq.Patterns
{
    public interface IProcessor : IDisposable
    {
        Task<Task> StartAsync();
        Task StopAsync();
    }
}