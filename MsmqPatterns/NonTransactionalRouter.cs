using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages between local <see cref = "MessageQueue"/>.  
    /// Note this is not safe in the event of process termination as a received message maybe lost.
    /// </summary>
    public class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(MessageQueue input, Func<Message, MessageQueue> route)
            : base (input, route)
        {
            Contract.Requires(input != null);
            Contract.Requires(route != null);
            Contract.Requires(!input.Transactional);
        }
        
        protected override void OnNewMessage(Message peeked)
        {
            try
            {
                RouteMessage(peeked);
            }
            catch (RouteException ex)
            {
                //TODO: logging
                Console.Error.WriteLine($"WARN {ex.Message} {{{ex.Destination?.FormatName}}}");
                BadMessageHandler(ex.LookupId, MessageQueueTransactionType.None);
            }            
        }

        private void RouteMessage(Message peeked)
        {
            var dest = GetRoute(peeked);

            using (var msg = _input.TryRecieve(StopTime))
            {
                if (msg == null) // message has been received by another process or thread
                    return;

                try
                {
                    dest.Send(msg);
                }
                catch (MessageQueueException ex)
                {
                    // we cannot send to that queue
                    throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest);
                }

            }
        }


    }
}