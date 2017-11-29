using System;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    /// <summary>
    /// Routes messages between local <see cref = "MessageQueue"/>.  
    /// Note this is not safe in the event of process termination as a received message maybe lost.
    /// </summary>
    public class NonTransactionalRouter : Router
    {
        public NonTransactionalRouter(MessageQueue input, MessageQueue deadletter, Func<Message, MessageQueue> route): base (input, deadletter, route)
        {
            Contract.Requires(route != null);
            Contract.Requires(!input.Transactional);
        }

        protected override async Task Run()
        {
            while (!_stop)
            {
                using (var msg = await _input.RecieveWithTimeoutAsync(StopTime))
                {
                    if (msg == null) // message has been received by another process or thread
                        continue;

                    var dest = _route(msg) ?? _deadLetter;
                    dest.Send(msg); //TODO: how to handle errors?
                }
            }
        }
    }
}