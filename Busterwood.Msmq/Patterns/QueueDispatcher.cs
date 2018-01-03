using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// Receives all messages from an input queue and invokes callback added via the <see cref="Subscribe(string, Action{Message})"/> method.
    /// Only works for non-transactional queues, really intended for use with multicast queues.
    /// </summary>
    public class QueueDispatcher : IProcessor
    {
        LabelSubscription _subscriptions;
        QueueReader _input;
        Task _running;

        /// <summary>Format name of the queue to route messages from</summary>
        public string InputQueueFormatName { get; }

        /// <summary>The path separator to use, defaults to dot (.)</summary>
        public char Separator
        {
            get { return _subscriptions.Separator; }
            set { _subscriptions.Separator = value; }
        }

        /// <summary>The wild-card string to use, defaults to star (*) </summary>
        public string WildCard
        {
            get { return _subscriptions.WildCard; }
            set { _subscriptions.WildCard = value; }
        }

        /// <summary>The all descendants wild-card string to use, defaults to star-star (**) </summary>
        public string AllDecendents
        {
            get { return _subscriptions.AllDecendents; }
            set { _subscriptions.AllDecendents = value; }
        }

        public QueueDispatcher(string inputQueueFormatName)
        {
            Contract.Requires(!string.IsNullOrEmpty(inputQueueFormatName));
            Contract.Requires(Queues.IsTransactional(inputQueueFormatName) == QueueTransactional.None);
            InputQueueFormatName = inputQueueFormatName;
            _subscriptions = new LabelSubscription();
        }

        public void Dispose()
        {
            try
            {
                StopAsync().Wait();
            }
            catch
            {
                // dispose should never throw exceptions
            }
        }

        public Task<Task> StartAsync()
        {
            _input = new QueueReader(InputQueueFormatName, share: QueueShareReceive.ExclusiveReceive);
            _running = RunAsync();
            return Task.FromResult(_running);
        }

        async Task RunAsync()
        {
            await Task.Yield();
            Properties peekProps = Properties.Label | Properties.LookupId;
            try
            {
                for (;;)
                {
                    Message msg = _input.Peek(peekProps, TimeSpan.Zero) ?? await _input.PeekAsync(peekProps);

                    // at this point we only have the label and lookup id of the message
                    var subscribers = _subscriptions.Subscribers(msg.Label);
                    if (subscribers == null)
                    {
                        // no subscribers, remove message from input queue anyway
                        _input.Lookup(Properties.LookupId, msg.LookupId, LookupAction.ReceiveCurrent, TimeSpan.Zero);
                        continue; 
                    }

                    // read the whole message now, remove it from the queue, and invoke the subscription callbacks
                    msg = _input.Lookup(Properties.All, msg.LookupId, LookupAction.ReceiveCurrent, TimeSpan.Zero); 
                    if (msg == null)
                    {
                        Console.Error.WriteLine($"WARNING: we peeked message but it was removed before we could read it {{label={msg.Label}, lookupId={msg.LookupId}}}");
                        continue;
                    }

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
        public IDisposable Subscribe(string label, Action<Message> callback)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));
            Contract.Requires(callback != null);
            return _subscriptions.Subscribe(label, callback);
        }
    }
}
