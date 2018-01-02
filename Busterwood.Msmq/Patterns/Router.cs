using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>A router of messages between <see cref="MessageQueue"/></summary>
    public abstract class Router : IProcessor
    {
        protected readonly Func<Message, QueueWriter> _route;
        protected QueueReader _input;
        protected SubQueue _posionQueue;
        Task _running;

        /// <summary>Format name of the queue to route messages from</summary>
        public string InputQueueFormatName { get; }

        /// <summary>Name of the subqueue to move messages to when they cannot be routed, or the router function returns null.</summary>
        public string UnroutableSubQueue { get; set; } = "Poison";

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public Properties PeekProperties { get; set; } = Properties.AppSpecific | Properties.Label | Properties.Extension | Properties.LookupId;

        public Postman Sender { get; }

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<QueueReader, long, QueueTransaction> BadMessageHandler { get; set; }

        protected Router(string inputQueueFormatName, Postman sender, Func<Message, QueueWriter> route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(sender != null);
            Contract.Requires(route != null);

            InputQueueFormatName = inputQueueFormatName;
            Sender = sender;
            _route = route;
            BadMessageHandler = MoveToPoisonSubqueue;
        }

        /// <summary>Starts the asynchronous routing process</summary>
        /// <returns></returns>
        public Task<Task> StartAsync()
        {
            _input = new QueueReader(InputQueueFormatName);
            _running = RunAsync();
            return Task.FromResult(_running);
        }

        protected abstract Task RunAsync();        

        protected QueueWriter GetRoute(Message msg)
        {
            QueueWriter r = null;
            try
            {
                r = _route(msg);
                if (r == null)
                    throw new NullReferenceException("route");
                return r;
            }
            catch (Exception ex)
            {
                throw new RouteException("Failed to get route", ex, msg.LookupId);
            }
        }

        public virtual Task StopAsync()
        {
            _input?.Dispose();
            _posionQueue?.Dispose();
            return _running;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
        }
        
        private void MoveToPoisonSubqueue(QueueReader fromQueue, long lookupId, QueueTransaction transaction)
        {
            Contract.Requires(fromQueue != null);
            if (_posionQueue == null)
            {
                _posionQueue = new SubQueue(InputQueueFormatName + ";" + UnroutableSubQueue);
            }
            try
            {
                Queues.MoveMessage(fromQueue, _posionQueue, lookupId, transaction);
                return;
            }
            catch (QueueException e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={UnroutableSubQueue}}} {{error={e.Message}}}");
            }            
        }

    }

}
