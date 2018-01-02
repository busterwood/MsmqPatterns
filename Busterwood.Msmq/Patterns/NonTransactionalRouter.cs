using System;
using System.Diagnostics.Contracts;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// Routes messages between non-transactional message queues.
    /// Note in the event of process termination message maybe routed TWICE, but will not be lost.
    /// </summary>
    public class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(string inputQueueFormatName, Postman sender, Func<Message, QueueWriter> route)
            : base (inputQueueFormatName, sender, route)
        {
            Contract.Requires(inputQueueFormatName != null);
            Contract.Requires(sender != null);
            Contract.Requires(route != null);
            PeekProperties = Properties.All;
        }

        protected override async Task RunAsync()
        {
            await Task.Yield();
            try
            {
                for (;;)
                {
                    var msg = _input.Peek(PeekProperties, TimeSpan.Zero) ?? await _input.PeekAsync(PeekProperties);
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
            }
        }

        async Task OnNewMessage(Message msg)
        {
            var lookupId = msg.LookupId;
            QueueWriter dest = null;
            try
            {
                dest = GetRoute(msg);
                await Sender.DeliverAsync(msg, dest, QueueTransaction.None);
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