using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>A router of messages between <see cref="MessageQueue"/></summary>
    public abstract class Router : IProcessor
    {
        protected readonly MessageQueue _input;
        protected readonly Func<Message, MessageQueue> _route;
        protected volatile bool _stop;
        Task _run;

        /// <summary>Timeout used when peeking for messages</summary>
        /// <remarks>the higher this value the slower the <see cref="StopAsync"/> method will be</remarks>
        public TimeSpan StopTime { get; set; } = TimeSpan.FromMilliseconds(100);


        /// <summary>The filter used when peeking messages, the default does NOT include the message body</summary>
        public MessagePropertyFilter PeekFilter { get; } = new MessagePropertyFilter
        {
            AppSpecific = true,
            Label = true,
            Extension = true,
            LookupId = true,
        };

        /// <summary>Handle messages that cannot be routed.  Defaults to moving messages to a "Poison" subqueue of the input queue</summary>
        public Action<long, bool?> BadMessageHandler { get; set; }

        /// <summary>
        /// Static factory method for creating the appropriate <see cref="Router"/> 
        /// based on the <see cref="MessageQueue.Transactional"/> property
        /// </summary>
        public static Router New(MessageQueue input, Func<Message, MessageQueue> route)
        {
            Contract.Requires(route != null);
            Contract.Requires(input != null);
            try
            {
                if (input.Transactional)
                    return new MsmqTransactionalRouter(input, route);
                else
                    return new NonTransactionalRouter(input, route);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.UnsupportedFormatNameOperation)
            {
                return new DtcTransactionalRouter(input, route);
            }
        }

        protected Router(MessageQueue input, Func<Message, MessageQueue> route)
        {
            Contract.Requires(input != null);
            Contract.Requires(route != null);

            _input = input;
            _route = route;
            BadMessageHandler = MoveToPoisonSubqueue;
        }

        /// <summary>Starts the asynchronous routing process</summary>
        /// <returns></returns>
        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            while (!_stop)
            {
                using (var msg = await TryPeekAsync())
                {
                    if (msg != null)
                        OnNewMessage(msg);
                }
            }
        }

        protected async Task<Message> TryPeekAsync()
        {
            var current = _input.MessageReadPropertyFilter; // save filter so it can be restored after peek
            try
            {
                _input.MessageReadPropertyFilter = PeekFilter;
                return await _input.TryPeekAsync(StopTime);
            }
            finally
            {
                _input.MessageReadPropertyFilter = current; // restore filter
            }
        }

        protected abstract void OnNewMessage(Message peeked);

        protected MessageQueue GetRoute(Message msg)
        {
            MessageQueue r = null;
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

        public Task StopAsync()
        {
            _stop = true;
            return _run;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
            _input?.Dispose();
        }

        private void MoveToPoisonSubqueue(long lookupId, bool? transactional = null)
        {
            const string poisonSubqueue = "Poison";
            try
            {
                _input.MoveMessage(poisonSubqueue, lookupId, transactional);
                return;
            }
            catch (Win32Exception e)
            {
                Console.Error.WriteLine($"WARN Failed to move message {{lookupId={lookupId}}} {{subqueue={poisonSubqueue}}} {{error={e.Message}}}");
            }            
        }

    }


}
