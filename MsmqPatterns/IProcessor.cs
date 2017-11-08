using System.Threading.Tasks;
namespace MsmqPatterns
{
    public interface IProcessor
    {
        Task<Task> StartAsync();
        Task StopAsync();
    }
}