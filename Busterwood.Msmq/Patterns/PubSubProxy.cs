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
        QueueReader _adminReader;
        Task _subscriptionTask;
        Task _dispatcherTask;
        Task _adminTask;
        FormatNameSubscriptions _subscriptions;
        string _adminQueueFormatName;

        /// <summary>Queue for receiving subscribe and unsubscribe requests</summary>
        public string ClientRequestQueueFormatName { get; }

        /// <summary>Data input queue, i.e. the multicast queue</summary>
        public string MulticastInputQueueFormatName { get; }

        /// <summary>Creates a new proxy</summary>
        /// <param name="clientRequestQueueFormatName">The queue to listen for subscribe and unsubscribe requests</param>
        /// <param name="multicastInputQueueFormatName">The queue contains the messages we want to subscribe to</param>
        public PubSubProxy(string clientRequestQueueFormatName, string multicastInputQueueFormatName)
        {
            Contract.Requires(clientRequestQueueFormatName != null);
            Contract.Requires(multicastInputQueueFormatName != null);
            ClientRequestQueueFormatName = clientRequestQueueFormatName;
            MulticastInputQueueFormatName = multicastInputQueueFormatName;
            _subscriptions = new FormatNameSubscriptions();
            _responseQueueCache = new QueueCache<QueueWriter>((fn, mode, share) => new QueueWriter(fn));
            _adminQueueFormatName = Queue.NewTempQueuePath();
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
            _adminReader = new QueueReader(_adminQueueFormatName, share: QueueShareReceive.ExclusiveReceive);
            _subscriptionTask = SubscriptionLoop();
            _dispatcherTask = MulticastInputDispatcher();
            _adminTask = AdminTask();
            return Task.FromResult(_subscriptionTask);
        }

        public Task StopAsync()
        {
            if (_clientRequestReader == null || _clientRequestReader.IsClosed)
                return Task.FromResult(true); // not started

            _clientRequestReader.Dispose();
            _inputReader.Dispose();
            _adminReader.Dispose();
            return Task.WhenAll(_subscriptionTask, _dispatcherTask, _adminTask);
        }

        /// <summary>Read messages from the input queue and forward to subscribers</summary>
        async Task MulticastInputDispatcher()
        {
            await Task.Yield();
            try
            {
                for (;;)
                {
                    var msg = _inputReader.Read(Properties.All, TimeSpan.Zero) ?? await _inputReader.ReadAsync(Properties.All);

                    // we could avoid the lock by using an immutable collection
                    HashSet<string> subscribers;
                    lock (_subscriptions)
                        subscribers = _subscriptions.Subscribers(msg.Label);

                    if (subscribers.Count <= 0)
                        continue;

                    var fn = string.Join(",", subscribers); // create a multi-element format name
                    var q = _responseQueueCache.Open(fn, QueueAccessMode.Send);

                    msg.AdministrationQueue = _adminQueueFormatName;
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

        /// <summary>Respond to requests to subscribe or unsubscribe</summary>
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
                        case PubSubProxyAction.UnsubscribeAll:
                            lock (_subscriptions)
                                _subscriptions.UnsubscribeAll(msg.ResponseQueue);
                            break;
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

        /// <summary>Report any failure to send to destination queues</summary>
        async Task AdminTask()
        {
            await Task.Yield();
            var props = Properties.Class | Properties.DestinationQueue | Properties.Label;
            try
            {
                for (;;)
                {
                    var msg = _adminReader.Read(props, TimeSpan.Zero) ?? await _adminReader.ReadAsync(props);
                    var ack = msg.Acknowledgement();
                    switch (ack)
                    {
                        case MessageClass.ReachQueueTimeout:
                        case MessageClass.AccessDenied:
                        case MessageClass.BadDestinationQueue:
                        case MessageClass.BadEncryption:
                        case MessageClass.BadSignature:
                        case MessageClass.CouldNotEncrypt:
                        case MessageClass.HopCountExceeded:
                        case MessageClass.NotTransactionalMessage:
                        case MessageClass.NotTransactionalQueue:
                        case MessageClass.Deleted:
                        case MessageClass.QueueDeleted:
                        case MessageClass.QueuePurged:
                        case MessageClass.QueueExceedQuota:
                        case MessageClass.ReceiveTimeout:
                            Console.Error.WriteLine($"WARNING {ack} sending '{msg.Label}' to {msg.DestinationQueue}");
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
        }

    }

    /// <summary>Set the request <see cref="Message.AppSpecific"/> to this value</summary>
    public enum PubSubProxyAction
    {
        Subscribe = 0,
        Unsubscribe = 1,
        //List = 5,
        UnsubscribeAll = 9,
    }
}
