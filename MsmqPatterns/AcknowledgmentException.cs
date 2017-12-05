using System;
using System.Messaging;
using System.Runtime.Serialization;

namespace MsmqPatterns
{
    [Serializable]
    public class AcknowledgmentException : Exception
    {
        public Acknowledgment Acknowledgment { get; }

        public AcknowledgmentException()
        {
        }

        public AcknowledgmentException(string message) : base(message)
        {
        }

        public AcknowledgmentException(string message, Acknowledgment acknowledgment) : base(message)
        {
            this.Acknowledgment = acknowledgment;
        }

        public AcknowledgmentException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected AcknowledgmentException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}