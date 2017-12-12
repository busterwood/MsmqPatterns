using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Transactions;
using BusterWood.Msmq;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages in a transaction. Up to <see cref="MaxBatchSize"/> messages are included in each transaction 
    /// because each transaction has a relatively large overhead.
    /// </summary>
    public abstract class TransactionalRouter : Router
    {
        protected Queue _inProgressRead;
        protected Queue _inProgressMove;
        private string inputQueueFormatName;
        private Func<Message, Queue> route;

        protected TransactionalRouter(string inputQueueFormatName, Sender sender, Func<Message, Queue> route)
            : base(inputQueueFormatName, sender, route)
        {
            Contract.Requires(sender != null);
            Contract.Requires(route != null);
            Contract.Requires(inputQueueFormatName != null);
        }

        /// <summary>
        /// Maximum number of messages batched into one transaction.
        /// We try to include multiple messages in a batch as it is MUCH faster (up to 10x)
        /// </summary>
        public int MaxBatchSize { get; set; } = 250;

        public string InProgressSubQueue { get; set; } = "batch";

        protected override Task RunAsync()
        {
            _inProgressRead = Queue.Open(InputQueueFormatName + ";" + InProgressSubQueue, QueueAccessMode.Receive);
            _inProgressMove = Queue.Open(InputQueueFormatName + ";" + InProgressSubQueue, QueueAccessMode.Move);
            return Task.FromResult(true);
        }

        public override Task StopAsync()
        {
            _inProgressRead?.Dispose();
            _inProgressMove?.Dispose();
            return base.StopAsync();
        }
    }
}
