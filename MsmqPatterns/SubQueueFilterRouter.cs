using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>
    /// Moves selected messages into sub-queues.
    /// This is a safe way to filter messages with explicit transaction control. The alternative, directly using a cursor, 
    /// is not safe as the cursor position does not update when a receive transaction is rolled-back.
    /// </summary>
    public class SubQueueFilterRouter : IProcessor
    {
        protected readonly MessageQueue _input;
        protected readonly Func<Message, string> _getSubQueueName;
        protected readonly TimeSpan _receiveTimeout = TimeSpan.FromMilliseconds(100);
        protected MessagePropertyFilter _peekFilter;
        volatile bool _stop;
        Task _run;

        public SubQueueFilterRouter(MessageQueue input, Func<Message, string> getSubQueueName)
        {
            Contract.Requires(input != null);
            Contract.Requires(getSubQueueName != null);
            _input = input;
            _getSubQueueName = getSubQueueName;
            _peekFilter = new MessagePropertyFilter
            {
                AppSpecific = true,
                Label = true,
                Extension = true,
                LookupId = true,
            };
        }
    
        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = RunAsync();
            return Task.FromResult(_run);
        }

        async Task RunAsync()
        {
            var action = PeekAction.Current;
            using (var cur = _input.CreateCursor())
            {
                while (!_stop)
                {
                    using (Message peeked = await PeekAsync(cur, action))
                    {
                        if (peeked == null)
                            continue;
                        var sqn = _getSubQueueName(peeked);
                        if (sqn != null)
                            _input.MoveMessage(sqn, peeked.LookupId);
                        action = PeekAction.Next;
                    }                
                }
            }
        }

        public Task StopAsync()
        {
            _stop = true;
            return _run;
        }

        async Task<Message> PeekAsync(Cursor cur, PeekAction action)
        {
            try
            {
                return await Task.Factory.FromAsync(_input.BeginPeek(_receiveTimeout, cur, action, null, null), _input.EndPeek);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
        }

    }

}
