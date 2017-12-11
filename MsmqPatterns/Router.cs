using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace MsmqPatterns
{
    /// <summary>A router of messages between <see cref="MessageQueue"/></summary>
    public abstract class Router : IProcessor
    {
        protected readonly string _inputQueueFormatName;
        protected readonly Func<Message, Queue> _route;
        protected Queue _input;
        Queue _posionQueue;
        Task _run;

        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public Properties PeekFilter { get; } = Properties.AppSpecific | Properties.Label | Properties.Extension | Properties.LookupId;

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<long, QueueTransaction> BadMessageHandler { get; set; }

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

        protected Router(string inputQueueFormatName, Func<Message, Queue> route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(route != null);

            _inputQueueFormatName = inputQueueFormatName;
            _route = route;
            BadMessageHandler = MoveToPoisonSubqueue;
        }

        /// <summary>Starts the asynchronous routing process</summary>
        /// <returns></returns>
        public Task<Task> StartAsync()
        {
            _input = Queue.Open(_inputQueueFormatName, QueueAccessMode.Receive);
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        protected virtual async Task RunAsync()
        {
            try
            {
                for(;;)
                {
                    var msg = await _input.PeekAsync(PeekFilter);
                    OnNewMessage(msg);
                }
            }
            catch (ObjectDisposedException) 
            {
                // Stop was called
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // Stop was called
            }
        }
        
        protected abstract void OnNewMessage(Message peeked);

        protected Queue GetRoute(Message msg)
        {
            Queue r = null;
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
        
        private void MoveToPoisonSubqueue(long lookupId, QueueTransaction transaction)
        {
            const string poisonSubqueue = "Poison";
            if (_posionQueue == null)
            {
                _posionQueue = Queue.Open(_inputQueueFormatName + ";Poison", QueueAccessMode.Move);
            }
            try
            {
                _input.Move(lookupId, _posionQueue, transaction);
                return;
            }
            catch (Win32Exception e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={poisonSubqueue}}} {{error={e.Message}}}");
            }            
        }

    }

}
