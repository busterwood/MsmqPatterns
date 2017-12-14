using System;
using System.Threading.Tasks;
namespace BusterWood.MsmqPatterns
{
    public interface IProcessor : IDisposable
    {
        Task<Task> StartAsync();
        Task StopAsync();
    }
}