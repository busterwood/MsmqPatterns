using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace MsmqPatterns
{
    /// <summary>A router of messages between <see cref="MessageQueue"/></summary>
    public abstract class Router : IProcessor
    {
        protected readonly Func<Message, QueueWriter> _route;
        protected QueueReader _input;
        protected SubQueueMover _posionQueue;
        Task _run;

        public string InputQueueFormatName { get; }

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public Properties PeekFilter { get; set; } = Properties.AppSpecific | Properties.Label | Properties.Extension | Properties.LookupId;

        public QueueSender Sender { get; }

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<QueueReader, long, QueueTransaction> BadMessageHandler { get; set; }

        ///// <summary>
        ///// Static factory method for creating the appropriate <see cref="Router"/> 
        ///// based on the <see cref="MessageQueue.Transactional"/> property
        ///// </summary>
        //public static Router New(Queue input, Func<Message, Queue> route)
        //{
        //    Contract.Requires(route != null);
        //    Contract.Requires(input != null);
        //    try
        //    {
        //        if (input.Transactional)
        //            return new MsmqTransactionalRouter(input, route);
        //        else
        //            return new NonTransactionalRouter(input, route);
        //    }
        //    catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.UnsupportedFormatNameOperation)
        //    {
        //        return new DtcTransactionalRouter(input, route);
        //    }
        //}

        protected Router(string inputQueueFormatName, QueueSender sender, Func<Message, QueueWriter> route)
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
            _run = RunAsync();
            return Task.FromResult(_run);
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
            return _run;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
        }
        
        private void MoveToPoisonSubqueue(QueueReader fromQueue, long lookupId, QueueTransaction transaction)
        {
            Contract.Requires(fromQueue != null);
            const string poisonSubqueue = "Poison";
            if (_posionQueue == null)
            {
                _posionQueue = new SubQueueMover(InputQueueFormatName + ";Poison");
            }
            try
            {
                _posionQueue.MoveFrom(fromQueue, lookupId, transaction);
                return;
            }
            catch (QueueException e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={poisonSubqueue}}} {{error={e.Message}}}");
            }            
        }

    }

}
