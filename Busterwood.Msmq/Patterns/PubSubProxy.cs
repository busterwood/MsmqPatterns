using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// A proxy for multicast queues with label based subscriptions.
    /// Often you multicast between a few servers, but don't want to multicast to hundreds of clients in case one slow client affects every clients multicast traffic.
    /// This proxy would live on a server, and clients subscribe and unsubscribe from multicast traffic by sending messages with the label and app-specific set.
    /// </summary>
    public class PubSubProxy : IProcessor
    {
        readonly QueueCache<QueueWriter> _responseQueueCache;
        QueueReader _clientRequestReader;
        QueueReader _inputReader;
        Task _subscriptionTask;
        Task _dispatcherTask;
        FormatNameSubscriptions _subscriptions;

        /// <summary>Queue for receiving subscribe and unsubscribe requests</summary>
        public string ClientRequestQueueFormatName { get; }

        /// <summary>Data input queue, i.e. the multicast queue</summary>
        public string MulticastInputQueueFormatName { get; }

        public PubSubProxy(string clientRequestQueueFormatName, string multicastInputQueueFormatName)
        {
            Contract.Requires(clientRequestQueueFormatName != null);
            Contract.Requires(multicastInputQueueFormatName != null);
            ClientRequestQueueFormatName = clientRequestQueueFormatName;
            MulticastInputQueueFormatName = multicastInputQueueFormatName;
            _subscriptions = new FormatNameSubscriptions();
            _responseQueueCache = new QueueCache<QueueWriter>((fn, mode, share) => new QueueWriter(fn));
        }

        public void Dispose()
        {
            try
            {
                StopAsync()?.Wait();
            }
            catch
            {
                // ignore all exceptions when disposing
            }
        }

        public Task<Task> StartAsync()
        {
            _clientRequestReader = new QueueReader(ClientRequestQueueFormatName, share: QueueShareReceive.ExclusiveReceive);
            _inputReader = new QueueReader(MulticastInputQueueFormatName);
            _subscriptionTask = SubscriptionLoop();
            _dispatcherTask = MulticastInputDispatcher();
            return Task.FromResult(_subscriptionTask);
        }

        async Task MulticastInputDispatcher()
        {
            await Task.Yield();
            try
            {
                for (;;)
                {
                    var msg = _inputReader.Read(Properties.All, TimeSpan.Zero) ?? await _inputReader.ReadAsync(Properties.All);

                    HashSet<string> subscribers;
                    lock (_subscriptions)
                        subscribers = _subscriptions.Subscribers(msg.Label);

                    if (subscribers.Count <= 0)
                        continue;

                    var fn = string.Join(",", subscribers); // create a multi-element format name
                    var q = _responseQueueCache.Open(fn, QueueAccessMode.Send);
                    q.Write(msg);
                }
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // stopped
            }
            catch (ObjectDisposedException)
            {
                // stopped
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR MulticastInputDispatcher: " + ex);
            }
        }

        //TODO: acknowledgement handling, i.e. wrong transaction type, access denied

        async Task SubscriptionLoop()
        {
            await Task.Yield();
            Properties props = Properties.AppSpecific | Properties.Label | Properties.ResponseQueue;
            try
            {
                for (;;)
                {
                    var msg = _clientRequestReader.Read(props, TimeSpan.Zero) ?? await _clientRequestReader.ReadAsync(props);
                    if (msg.ResponseQueue.Length == 0)
                    {
                        Console.Error.WriteLine("Request with no response queue, ignoring: " + msg.Label);
                        continue;
                    }
                    switch ((PubSubProxyAction)msg.AppSpecific)
                    {
                        case PubSubProxyAction.Subscribe:
                            lock(_subscriptions)
                                _subscriptions.Subscribe(msg.Label, msg.ResponseQueue);
                            break;
                        case PubSubProxyAction.Unsubscribe:
                            lock (_subscriptions)
                                _subscriptions.Unsubscribe(msg.Label, msg.ResponseQueue);
                            break;
                        //TODO: unsubscribe all for a msg.ResponseQueue
                        default:
                            Console.Error.WriteLine($"Request with invalid {nameof(msg.AppSpecific)} {msg.AppSpecific}, ignoring request for '{msg.Label}' for '{msg.ResponseQueue}'");
                            break;
                    }
                }
            }
            catch (QueueException ex) when (ex.ErrorCode == ErrorCode.OperationCanceled)
            {
                // stopped
            }
            catch (ObjectDisposedException)
            {
                // stopped
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR SubscriptionLoop: " + ex);
            }
        }

        public Task StopAsync()
        {
            _clientRequestReader?.Dispose();
            _inputReader?.Dispose();
            if (_subscriptionTask == null && _dispatcherTask == null)
                return Task.FromResult(true);
            return Task.WhenAll(_subscriptionTask, _dispatcherTask);

        }
        
    }

    /// <summary>
    /// Set the request <see cref="Message.AppSpecific"/> to this value
    /// </summary>
    public enum PubSubProxyAction
    {
        Subscribe = 0,
        Unsubscribe = 1,
        UnsubscribeAll = 9,
    }
}
