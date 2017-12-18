using BusterWood.Msmq;
using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace BusterWood.MsmqPatterns
{
    /// <summary>
    /// Receives all messages from an input queue and invokes callback added via the <see cref="Subscribe(string, Action{Message})"/> method.
    /// </summary>
    public class NonTransactionalDispatcher : IProcessor
    {
        LabelSubscription _subscriptions;
        QueueReader _input;
        Task _running;

        /// <summary>Format name of the queue to route messages from</summary>
        public string InputQueueFormatName { get; }

        public NonTransactionalDispatcher(string inputQueueFormatName)
        {
            Contract.Requires(!string.IsNullOrEmpty(inputQueueFormatName));
            Contract.Requires(Queue.IsTransactional(inputQueueFormatName) == QueueTransactional.None);
            InputQueueFormatName = inputQueueFormatName;
            _subscriptions = new LabelSubscription();
        }

        public void Dispose()
        {
        }

        public Task<Task> StartAsync()
        {
            _input = new QueueReader(InputQueueFormatName, share: QueueShareReceive.ExclusiveReceive);
            _running = RunAsync();
            return Task.FromResult(_running);
        }

        async Task RunAsync()
        {
            Properties peekFilter = Properties.Label | Properties.LookupId;
            try
            {
                for (;;)
                {
                    Message msg = _input.Peek(peekFilter, TimeSpan.Zero); // peek next without waiting
                    if (msg == null)
                        msg = await _input.PeekAsync(peekFilter); // no message, we must wait

                    // at this point we only have the label and lookup id of the message
                    var subscribers = _subscriptions.Subscribers(msg.Label);
                    if (subscribers == null)
                    {
                        // no subscribers, remove message from input queue anyway
                        _input.Lookup(peekFilter, msg.LookupId, LookupAction.ReceiveCurrent, TimeSpan.Zero);
                        continue; 
                    }

                    // read the whole message now, remove it from the queue, and invoke the subscription callbacks
                    msg = _input.Lookup(Properties.All, msg.LookupId, LookupAction.ReceiveCurrent, TimeSpan.Zero); 
                    _subscriptions.TryInvokeAll(msg, subscribers);
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

        public Task StopAsync()
        {
            _input.Dispose();
            return _running;
        }

        /// <summary>
        /// Subscribe to messages with a matching <paramref name="label"/>, invoking the <paramref name="callback"/> when a new message arrives on the input queue.
        /// You can subscribe with <see cref="LabelSubscription.WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
        /// You can subscribe with <see cref="LabelSubscription.AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
        /// </summary>
        /// <returns>A handle to that unsubscribes when it is Disposed</returns>
        public IDisposable Subscribe(string label, Action<Message> callback) => _subscriptions.Subscribe(label, callback);
    }
}
