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

        public RequestReply(string requestQueue, string replyQueue, string adminQueue) 
            : this(new MessageQueue(requestQueue, QueueAccessMode.Send), new MessageQueue(replyQueue, QueueAccessMode.Receive), new MessageQueue(adminQueue, QueueAccessMode.Receive))
        { }

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

        public Message SendRequest(Message request)
        {
            Contract.Requires(request != null);
            Contract.Ensures(Contract.Result<Message>() != null);
            var sw = new Stopwatch();
            sw.Start();

            request.Recoverable = false; // express mode
            request.ResponseQueue = _responseQueue;

            // setup timeout with negative acknowledgement
            request.TimeToBeReceived = TimeSpan.FromSeconds(30);
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

            try
            {
                //TODO: how long do we wait for a response?
                return _responseQueue.ReceiveByCorrelationId(request.Id, MessageQueue.InfiniteTimeout);
            }
            catch (MessageQueueException e) when (e.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                throw new TimeoutException();
            }
            finally
            {
                Console.WriteLine($"Getting reply took {sw.ElapsedMilliseconds:N0}ms");
            }
        }

        public async Task<Message> SendRequestAsync(Message request)
        {
            Contract.Requires(request != null);
            var sw = new Stopwatch();
            sw.Start();

            request.Recoverable = false; // express mode
            request.ResponseQueue = _responseQueue;

            // setup timeout with negative acknowledgement
            request.TimeToBeReceived = TimeSpan.FromSeconds(30);
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
            
            try
            {
                //TODO: how long do we wait for a response?
                return await _responseQueue.ReceiveByCorrelationIdAsync(request.Id);
            }
            catch (MessageQueueException e) when (e.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            {
                throw new TimeoutException();
            }
            finally
            {
                Console.WriteLine($"Getting reply took {sw.ElapsedMilliseconds:N0}ms");
            }
        }
    }
    
}
