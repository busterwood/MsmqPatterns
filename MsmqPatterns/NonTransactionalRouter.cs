using System;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace BusterWood.MsmqPatterns
{
    /// <summary>
    /// Routes messages between local <see cref = "MessageQueue"/>.  
    /// Note this is not safe in the event of process termination as a received message maybe lost.
    /// </summary>
    public class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(string inputQueueFormatName, Postman sender, Func<Message, QueueWriter> route)
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

        async Task OnNewMessage(Message msg)
        {
            var lookupId = msg.LookupId;
            QueueWriter dest = null;
            try
            {
                dest = GetRoute(msg);
                await Sender.DeliverAsync(msg, QueueTransaction.None, dest);
                var removed = _input.Lookup(Properties.LookupId, lookupId, timeout: TimeSpan.Zero); // remove message from queue
                if (removed == null)
                    Console.Error.WriteLine($"WARN: router message to {dest.FormatName} but could not remove message from input queue");
            }
            catch (QueueException ex)
            {
                //TODO: logging
                Console.Error.WriteLine($"WARN {ex.Message} {{{dest?.FormatName}}}");
                BadMessageHandler(_input, lookupId, QueueTransaction.None);
            }
            catch (RouteException ex)
            {
                //TODO: logging
                Console.Error.WriteLine($"WARN {ex.Message} {{{ex.Destination}}}");
                BadMessageHandler(_input, ex.LookupId, QueueTransaction.None);
            }            
        }


    }
}