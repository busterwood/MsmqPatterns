﻿using System;
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
        protected readonly TimeSpan _receiveTimeout = TimeSpan.FromSeconds(0.1);
        protected MessagePropertyFilter _peekFilter;
        protected readonly Func<Message, MessageQueue> _route;
        volatile bool _stop;
        Task _run;
        
        /// <summary>
        /// Static factory method for creating the appropriate <see cref="Router"/> 
        /// based on the <see cref="MessageQueue.Transactional"/> property
        /// </summary>
        public static Router New(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
        {
            try
            {
                if (input.Transactional)
                    return new TransactionalRouter(input, deadletter, route);
                else
                    return new NonTransactionalRouter(input, deadletter, route);
            }
            catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.UnsupportedFormatNameOperation)
            {
                return new DtcTransactionalRouter(input, deadletter, route);
            }
        }

        protected Router(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route)
        {
            Contract.Requires(input != null);
            Contract.Requires(deadletter != null);
            Contract.Requires(!deadletter.Transactional);

            _input = input;
            _deadLetter = deadletter;
            _route = route;

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

        protected abstract void RouteMessage(Message peeked);
    }


    /// <summary>Routes messages between local <see cref="MessageQueue"/></summary>
    public class NonTransactionalRouter : Router
    {

        public NonTransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route) 
            : base(input, deadletter, route)
        {
            Contract.Requires(route != null);
            Contract.Requires(!input.Transactional);
        }

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
            using (msg)
            {
                var dest = _route(msg) ?? _deadLetter;
                try
                {
                    dest.Send(msg);
                }
                catch (Exception)
                {
                    return; //TODO: we cannot process this message, what to do?
                }
            }
        }
    }
    
    /// <summary>Routes messages between local <see cref="MessageQueue"/> using a local MSMQ transaction</summary>
    public class TransactionalRouter : Router
    {
        public TransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route) 
            : base(input, deadletter, route)
        {
            Contract.Requires(input.Transactional);
            Contract.Requires(deadletter.Transactional);
        }

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
                using (msg)
                {
                    var dest = _route(msg) ?? _deadLetter;
                    try
                    {
                        dest.Send(msg, txn);
                        txn.Commit();
                    }
                    catch (Exception)
                    {
                        return; //TODO: we cannot process this message, what to do?
                    }
                }
            }
        }
    }


    /// <summary>Routes messages in local or remote queues using DTC <see cref="TransactionScope"/></summary>
    public class DtcTransactionalRouter : Router
    {
        public DtcTransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route) 
            : base(input, deadletter, route) { }

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

                using (msg)
                {
                    var dest = _route(msg) ?? _deadLetter;
                    try
                    {
                        dest.Send(msg, MessageQueueTransactionType.Automatic);
                        txn.Complete();
                    }
                    catch (Exception)
                    {
                        return; //TODO: we cannot process this message, what to do?
                    }
                }
            }
        }
    }
}
