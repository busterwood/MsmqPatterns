using System;
using System.Runtime.Serialization;

namespace Busterwood.Msmq
{
    [Serializable]
    internal class QueueException : Exception
    {
        public int ErrorCode { get; }

        public QueueException()
        {
        }

        public QueueException(string message) : base(message)
        {
        }

        public QueueException(int errorCode)
        {
            this.ErrorCode = errorCode;
        }

        public QueueException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected QueueException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}