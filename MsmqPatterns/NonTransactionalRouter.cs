using System;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages between local <see cref = "MessageQueue"/>.  
    /// Note this is not safe in the event of process termination as a received message maybe lost.
    /// </summary>
    public class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(string inputQueueFormatName, Sender sender, Func<Message, QueueWriter> route)
            : base (inputQueueFormatName, sender, route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(sender != null);
            Contract.Requires(route != null);
            PeekFilter = Properties.All;
        }

        protected override async Task RunAsync()
        {
            try
            {
                for (;;)
                {
                    var msg = await _input.PeekAsync(PeekFilter);
                    await OnNewMessage(msg);
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
            catch (Exception ex)
            {
                Console.Error.WriteLine("WARNING: " + ex);
                throw;
            }
        }

        async Task OnNewMessage(Message peeked)
        {
            try
            {
                await RouteMessage(peeked);
            }
            catch (RouteException ex)
            {
                //TODO: logging
                Console.Error.WriteLine($"WARN {ex.Message} {{{ex.Destination}}}");
                BadMessageHandler(_input, ex.LookupId, QueueTransaction.None);
            }            
        }

        private async Task RouteMessage(Message msg)
        {
            var dest = GetRoute(msg);
            try
            {
                await Sender.SendAsync(msg, QueueTransaction.None, dest);
                _input.Receive(Properties.LookupId, timeout: TimeSpan.Zero); // remove message from queue
            }
            catch (QueueException ex)
            {
                // we cannot send to that queue
                throw new RouteException("Failed to send to destination", ex, msg.LookupId, dest.FormatName);
            }
        }


    }
}