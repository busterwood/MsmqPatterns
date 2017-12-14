using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace BusterWood.MsmqPatterns
{
    public class RequestReply : IDisposable
    {
        readonly QueueWriter _requestQueue;
        readonly QueueReader _responseQueue;
        readonly Postman _postman; // for receive and delivery notifications
        
        /// <summary>Max time for the request to be received by the process that handles the request.  Defaults to 30 seconds</summary>
        public TimeSpan TimeToBeReceived { get; set; } = TimeSpan.FromSeconds(30);

        public RequestReply(string requestQueueFormantName, string replyQueueFormatName, Postman postman) 
        {
            Contract.Requires(postman != null);
            Contract.Requires(requestQueueFormantName != null);
            Contract.Requires(replyQueueFormatName != null);

            _requestQueue = new QueueWriter(requestQueueFormantName);
            _responseQueue = new QueueReader(replyQueueFormatName);
            _postman = postman;
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

            SetupRequest(request);
            var tracking = _postman.RequestDelivery(request, null, _requestQueue);
            _postman.WaitForDelivery(tracking).Wait();
            _postman.WaitToBeReceived(tracking).Wait();
            return _responseQueue.ReadByCorrelationId(request.Id);
        }

        private void SetupRequest(Message request)
        {
            request.Delivery = Delivery.Express;
            request.ResponseQueue = _responseQueue.FormatName;

            // setup timeout with negative acknowledgement
            request.TimeToBeReceived = TimeToBeReceived;
            request.AcknowledgmentTypes = AcknowledgmentTypes.FullReachQueue | AcknowledgmentTypes.FullReceive;
        }

        /// <summary>Sends a request message and waits for a reply.</summary>
        /// <param name="request">The message to send</param>
        /// <returns>The reply message</returns>
        /// <exception cref="TimeoutException">Thrown when the message has not been received by the target processor before <see cref="TimeToBeReceived"/></exception>
        /// <exception cref="AcknowledgmentException">Thrown when the message cannot be delivered to the destination queue</exception>
        public async Task<Message> SendRequestAsync(Message request)
        {
            Contract.Requires(request != null);

            SetupRequest(request);
            var tracking = _postman.RequestDelivery(request, null, _requestQueue);
            await _postman.WaitForDelivery(tracking);
            await _postman.WaitToBeReceived(tracking);
            return await _responseQueue.ReadByCorrelationIdAsync(request.Id);
        }

        public void Dispose()
        {
            _requestQueue.Dispose();
            _responseQueue.Dispose();
        }
    }
    
}
