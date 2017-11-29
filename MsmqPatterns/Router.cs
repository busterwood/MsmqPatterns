using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>A router of messages between <see cref="MessageQueue"/></summary>
    public abstract class Router : IProcessor
    {
        protected readonly MessageQueue _input;
        protected readonly MessageQueue _deadLetter;
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

        /// <summary>
        /// Static factory method for creating the appropriate <see cref="Router"/> 
        /// based on the <see cref="MessageQueue.Transactional"/> property
        /// </summary>
        public static Router New(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
        {
            try
            {
                if (input.Transactional)
                    return new MsmqTransactionalRouter(input, deadletter, route);
                else
                    return new NonTransactionalRouter(input, deadletter, route);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.UnsupportedFormatNameOperation)
            {
                return new DtcTransactionalRouter(input, deadletter, route);
            }
        }

        protected Router(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
        {
            Contract.Requires(input != null);
            Contract.Requires(deadletter != null);
            Contract.Requires(!deadletter.Transactional);

            _input = input;
            _deadLetter = deadletter;
            _route = route;
        }

        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        protected abstract Task RunAsync();

        protected MessageQueue GetRoute(Message msg) => _route(msg) ?? _deadLetter; //TODO: what if route fails?

        public Task StopAsync()
        {
            _stop = true;
            return _run;
        }

        public void Dispose()
        {
            StopAsync()?.Wait();
            _input?.Dispose();
            _deadLetter?.Dispose();
        }
       
    }
    
}
