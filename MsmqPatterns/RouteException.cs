using System;
using System.Messaging;
using System.Runtime.Serialization;

namespace MsmqPatterns
{
    [Serializable]
    public class RouteException : Exception
    {
        public MessageQueue Destination { get; }

        public long LookupId { get; }            

        public RouteException()
        {
        }

        public RouteException(string message) : base(message)
        {
        }

        public RouteException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public RouteException(string message, Exception innerException, long lookupId) : this(message, innerException)
        {
            this.LookupId = lookupId;
        }

        protected RouteException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public RouteException(string message, Exception innerException, long lookupId, MessageQueue dest) : this(message, innerException, lookupId)
        {
            this.Destination = dest;
        }
    }
}