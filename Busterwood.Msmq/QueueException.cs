using System;
using System.Runtime.Serialization;

namespace BusterWood.Msmq
{
    [Serializable]
    internal class QueueException : Exception
    {
        public ErrorCode ErrorCode { get; }

        public QueueException()
        {
        }

        public QueueException(string message) : base(message)
        {
        }

        public QueueException(int errorCode) : base($"Queue exception: {(ErrorCode)errorCode}")
        {
            ErrorCode = (ErrorCode)errorCode;
        }

        public QueueException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected QueueException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}