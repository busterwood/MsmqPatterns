using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Messaging;
using System.Threading.Tasks;

namespace MsmqPatterns
{
    public class RequestReply
    {
        readonly MessageQueue _requestQueue;
        readonly MessageQueue _responseQueue;
        readonly MessageQueue _adminQueue; // for timeout acknowledgements

        /// <summary>Max time for the request to be received by the process that handles the request.  Defaults to 30 seconds</summary>
        public TimeSpan TimeToBeReceived { get; set; } = TimeSpan.FromSeconds(30);

        public RequestReply(string requestQueue, string replyQueue, string adminQueue) 
            : this(new MessageQueue(requestQueue, QueueAccessMode.Send), new MessageQueue(replyQueue, QueueAccessMode.Receive), new MessageQueue(adminQueue, QueueAccessMode.Receive))
        {
            Contract.Requires(adminQueue != null);
            Contract.Requires(replyQueue != null);
            Contract.Requires(requestQueue != null);
        }

        public RequestReply(MessageQueue requestQueue, MessageQueue replyQueue, MessageQueue adminQueue)
        {
            Contract.Requires(adminQueue != null);
            Contract.Requires(replyQueue != null);
            Contract.Requires(requestQueue != null);
            _requestQueue = requestQueue;
            _responseQueue = replyQueue;
            _adminQueue = adminQueue;
            _responseQueue.MessageReadPropertyFilter = new MessagePropertyFilter { Body = true, AppSpecific = true, CorrelationId = true, Label = true, Extension = true };
        }

        /// <summary>Sends a request message and waits for a reply.</summary>
        /// <param name="request">The message to send</param>
        /// <returns>The reply message</returns>
        /// <exception cref="TimeoutException">Thrown when the message has not been received by the target processor before <see cref="TimeToBeReceived"/></exception>
        /// <exception cref="AcknowledgmentException">Thrown when the message cannot be delivered to the destination queue</exception>
        public Message SendRequest(Message request)
        {
            Contract.Requires(request != null);
            Contract.Ensures(Contract.Result<Message>() != null);

            request.Recoverable = false; // express mode
            request.ResponseQueue = _responseQueue;

            // setup timeout with negative acknowledgement
            request.TimeToBeReceived = TimeToBeReceived;
            request.AcknowledgeType = AcknowledgeTypes.NegativeReceive | AcknowledgeTypes.FullReceive | AcknowledgeTypes.NotAcknowledgeReceive | AcknowledgeTypes.NotAcknowledgeReachQueue;
            request.AdministrationQueue = _adminQueue;

            _requestQueue.Send(request);

            // wait for acknowledgement of receive on the admin queue
            using (Message ack = _adminQueue.ReceiveByCorrelationId(request.Id, MessageQueue.InfiniteTimeout))
            {
                switch (ack.Acknowledgment)
                {
                    case Acknowledgment.ReceiveTimeout:
                        throw new TimeoutException();
                    case Acknowledgment.Receive:
                        break;
                    default:
                        throw new AcknowledgmentException(ack.Acknowledgment);
                }
            }

            //TODO: maybe timeout the processing?
            return _responseQueue.ReceiveByCorrelationId(request.Id, MessageQueue.InfiniteTimeout);
        }

        /// <summary>Sends a request message and waits for a reply.</summary>
        /// <param name="request">The message to send</param>
        /// <returns>The reply message</returns>
        /// <exception cref="TimeoutException">Thrown when the message has not been received by the target processor before <see cref="TimeToBeReceived"/></exception>
        /// <exception cref="AcknowledgmentException">Thrown when the message cannot be delivered to the destination queue</exception>
        public async Task<Message> SendRequestAsync(Message request)
        {
            Contract.Requires(request != null);

            request.Recoverable = false; // express mode
            request.ResponseQueue = _responseQueue;

            // setup timeout with negative acknowledgement
            request.TimeToBeReceived = TimeToBeReceived;
            request.AcknowledgeType = AcknowledgeTypes.NegativeReceive | AcknowledgeTypes.FullReceive | AcknowledgeTypes.NotAcknowledgeReceive | AcknowledgeTypes.NotAcknowledgeReachQueue;
            request.AdministrationQueue = _adminQueue;

            _requestQueue.Send(request);

            // wait for acknowledgement of receive on the admin queue
            using (Message ack = await _adminQueue.ReceiveByCorrelationIdAsync(request.Id))
            {
                switch (ack.Acknowledgment)
                {
                    case Acknowledgment.ReceiveTimeout:
                        throw new TimeoutException();
                    case Acknowledgment.Receive:
                        break;
                    default:
                        throw new AcknowledgmentException(ack.Acknowledgment);
                }
            }

            //TODO: maybe timeout the processing?
            return await _responseQueue.ReceiveByCorrelationIdAsync(request.Id);            
        }
    }
    
}
