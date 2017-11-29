using System;
using System.Threading.Tasks;
namespace MsmqPatterns
{
    public interface IProcessor : IDisposable
    {
        Task<Task> StartAsync();
        Task StopAsync();
    }
}