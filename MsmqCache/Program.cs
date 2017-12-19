using BusterWood.Msmq.Patterns;
using System;

namespace BusterWood.Msmq.Cache
{
    class Program
    {
        static void Main(string[] args)
        {
            //TODO: configuration of multiple caches for different input queues, via config file?
            var inputFN = Queue.TryCreate(".\\private$\\cache.input", QueueTransactional.None);
            var adminFN = Queue.TryCreate(".\\private$\\cache.admin", QueueTransactional.None);
            var mc = new MessageCache(inputFN, adminFN, null, TimeSpan.FromDays(1));
            mc.StartAsync();
            Console.WriteLine("Started cache listening on " + mc.InputQueueFormatName);
            Console.WriteLine("press ENTER to exit");
            Console.ReadLine();
            mc.Dispose();
        }
    }
}
