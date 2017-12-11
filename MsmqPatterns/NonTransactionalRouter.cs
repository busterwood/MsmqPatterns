using System;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages between local <see cref = "MessageQueue"/>.  
    /// Note this is not safe in the event of process termination as a received message maybe lost.
    /// </summary>
    public class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(string inputQueueFormatName, Func<Message, Queue> route)
            : base (inputQueueFormatName, route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(route != null);
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
                Console.Error.WriteLine($"WARN {ex.Message} {{{ex.Destination}}}");
                BadMessageHandler(ex.LookupId, QueueTransaction.None);
            }            
        }

        private void RouteMessage(Message peeked)
        {
            var dest = GetRoute(peeked);

            var msg = _input.Receive(Properties.All, timeout: TimeSpan.Zero);
            if (msg == null) // message has been received by another process or thread
                return;

            try
            {
                dest.Post(msg);
            }
            catch (QueueException ex)
            {
                // we cannot send to that queue
                throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest.FormatName);
            }
        }


    }
}