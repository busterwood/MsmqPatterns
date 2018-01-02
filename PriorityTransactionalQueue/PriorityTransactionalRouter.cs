using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Msmq.Patterns;

namespace BusterWood.Msmq.Examples
{
    /// <summary>
    /// Sample router to that moves messages to "low" or "high" subqueue, depending on the value of <see cref="Message.AppSpecific"/>
    /// </summary>
    public class PriorityTransactionalRouter : IProcessor
    {
        SubQueueRouter _router;
        SubQueue _low;
        SubQueue _high;

        public string InputQueueFormatName { get; }

        public PriorityTransactionalRouter(string inputQueueFormatName)
        {
            Contract.Requires(inputQueueFormatName != null);
            if (Queues.IsTransactional(inputQueueFormatName) == QueueTransactional.None)
                throw new ArgumentException(inputQueueFormatName + " is not a transactional queue");

            InputQueueFormatName = inputQueueFormatName;
            _router = new SubQueueRouter(inputQueueFormatName, RouterByPriority);
        }

        /// <summary>move the messages into "high" or "low" subqueues based on the value of <see cref="Message.AppSpecific"/></summary>
        private SubQueue RouterByPriority(Message arg)
        {
            return arg.AppSpecific == 0 ? _low : _high;
        }

        public void Dispose()
        {
            _router.Dispose();
            _low?.Dispose();
            _high?.Dispose();
        }

        public Task<Task> StartAsync()
        {
            _low = new SubQueue(InputQueueFormatName + ";low");
            _high = new SubQueue(InputQueueFormatName + ";high");
            return _router.StartAsync();
        }

        public Task StopAsync() => _router.StopAsync();
    }
}
