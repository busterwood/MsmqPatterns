﻿using System;
using System.Runtime.Serialization;

namespace BusterWood.Msmq.Patterns
{
    [Serializable]
    public class RouteException : Exception
    {
        /// <summary>The format name of the destination</summary>
        public string Destination { get; }

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

        public RouteException(string message, Exception innerException, long lookupId, string dest) : this(message, innerException, lookupId)
        {
            this.Destination = dest;
        }
    }
}