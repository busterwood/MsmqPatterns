using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;
using System.Transactions;

namespace MsmqPatterns
{

    /// <summary>A router of messages between <see cref="MessageQueue"/></summary>
    public abstract class Router : IProcessor
    {
        protected readonly MessageQueue _input;
        protected readonly MessageQueue _deadLetter;
        protected readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(0.5);
        protected MessagePropertyFilter _peekFilter;
        volatile bool _stop;
        Task _run;

        public Router(MessageQueue deadletter)
        {
            Contract.Requires(deadletter != null);
            _peekFilter = new MessagePropertyFilter
            {
                AppSpecific = true,
                Label = true,
                Extension = true,
            };
        }

        public Task<Task> StartAsync()
        {
            _stop = false;
            _run = Run();
            return Task.FromResult(_run);
        }

        async Task Run()
        {
            while (!_stop)
            {
                using (Message peeked = await PeekAsync())
                {
                    if (peeked != null)
                        RouteMessage(peeked);
                }
            }
        }

        public Task StopAsync()
        {
            _stop = true;
            return _run;
        }

        private async Task<Message> PeekAsync()
        {
            var current = _input.MessageReadPropertyFilter; // save filter so it can be restored after peek
            try
            {
                _input.MessageReadPropertyFilter = _peekFilter;
                return await Task.Factory.FromAsync(_input.BeginPeek(_receiveTimeout), _input.EndPeek);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                return null;
            }
            finally
            {
                _input.MessageReadPropertyFilter = current; // restore filter
            }
        }

        protected abstract MessageQueue GetRoute(Message peeked);

        protected abstract void RouteMessage(Message peeked);
    }


    /// <summary>Routes messages between local <see cref="MessageQueue"/></summary>
    public abstract class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(MessageQueue deadletter) : base(deadletter) { }

        protected override void RouteMessage(Message peeked)
        {
            Message msg;
            try
            {
                msg = _input.Receive(_receiveTimeout);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                // message has been received by another process or thread
                return;
            }

            var dest = GetRoute(msg) ?? _deadLetter;
            try
            {
                dest.Send(msg);
            }
            catch (Exception)
            {
                return; //TODO: we cannot process this message, what to do?
            }
            finally
            {
                msg.Dispose();
            }
        }
    }
    
    /// <summary>Routes messages between local <see cref="MessageQueue"/></summary>
    public abstract class TransactionalRouter : Router
    {
        public TransactionalRouter(MessageQueue deadletter) : base(deadletter) { }

        protected override void RouteMessage(Message peeked)
        {
            using (var txn = new MessageQueueTransaction())
            {
                txn.Begin();
                Message msg;
                try
                {
                    msg = _input.Receive(_receiveTimeout, txn);
                }
                catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                {
                    // message has been received by another process or thread
                    return;
                }

                var dest = GetRoute(msg) ?? _deadLetter;
                try
                {
                    dest.Send(msg, txn);
                    txn.Commit();
                }
                catch (Exception)
                {
                    return; //TODO: we cannot process this message, what to do?
                }
                finally
                {
                    msg.Dispose();
                }
            }
        }
    }


    ///// <summary>Routes messages within the same queue</summary>
    //public abstract class SubQueueTxnRouter : Router
    //{
    //    public SubQueueTxnRouter(MessageQueue deadletter) : base(deadletter)
    //    {
    //        _peekFilter.LookupId = true; // needed for move
    //    }

    //    protected override void RouteMessage(Message peeked)
    //    {
    //        var dest = GetRoute(peeked) ?? _deadLetter;
    //        _input.MoveMessage(dest, peeked.LookupId, true);
    //        //TODO: handle failure due to another process moving the message, or the queue being purged
    //    }
    //}


    /// <summary>Routes messages in local or remote queues using DTC <see cref="TransactionScope"/></summary>
    public abstract class DtcTransactionalRouter : Router
    {
        public DtcTransactionalRouter(MessageQueue deadletter) : base(deadletter) { }

        protected override void RouteMessage(Message peeked)
        {
            using (var txn = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                Message msg;
                try
                {
                    msg = _input.Receive(_receiveTimeout, MessageQueueTransactionType.Automatic);
                }
                catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                {
                    // message has been received by another process or thread
                    return;
                }

                var dest = GetRoute(msg) ?? _deadLetter;
                try
                {
                    dest.Send(msg, MessageQueueTransactionType.Automatic);
                    txn.Complete();
                }
                catch (Exception)
                {
                    return; //TODO: we cannot process this message, what to do?
                }
                finally
                {
                    msg.Dispose();
                }
            }
        }
    }
}
