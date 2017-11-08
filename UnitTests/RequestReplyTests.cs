using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Messaging;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class RequestReplyTests
    {
        readonly string requestQueueName = $".\\private$\\{nameof(RequestReplyTests)}.Request";
        readonly string replyQueueName = $".\\private$\\{nameof(RequestReplyTests)}.Reply";
        readonly string adminQueueName = $".\\private$\\{nameof(RequestReplyTests)}.Admin";

        [SetUp]
        public void Setup()
        {
            CreateOrClear(requestQueueName);
            CreateOrClear(replyQueueName);
            CreateOrClear(adminQueueName);
        }

        private void CreateOrClear(string qname)
        {
            if (MessageQueue.Exists(qname))
            {
                ReadAllMessages(qname);
            }
            else
                MessageQueue.Create(qname);
        }

        private void ReadAllMessages(string path)
        {
            using (var q = new MessageQueue(path, QueueAccessMode.Receive))
            {
                for (;;)
                {
                    try
                    {
                        q.Receive(TimeSpan.FromMilliseconds(50)).Dispose();
                    }
                    catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                    {
                        break;
                    }
                }
            }
        }

        [Test, Timeout(1000)]
        public async Task can_get_reply()
        {
            var key = Environment.TickCount;
            var rr = new RequestReply(requestQueueName, replyQueueName, adminQueueName);

            using (var request = new Message { Label = "my.sq", AppSpecific = key, Recoverable = false })
            {
                var serverTask = Task.Run(() => Server(1));
                using (var reply = await rr.SendRequestAsync(request))
                {
                    Assert.AreEqual(request.Label, reply.Label);
                    Assert.AreEqual(request.AppSpecific, reply.AppSpecific);
                }
            }
        }


        [Test, Timeout(1000)]
        public async Task can_get_multiple_replies()
        {
            var key = Environment.TickCount;
            var rr = new RequestReply(requestQueueName, replyQueueName, adminQueueName);

            var serverTask = Task.Run(() => Server(10));

            for (int i = 0; i < 10; i++)
            {
                using (var request = new Message { Label = "my.sq", AppSpecific = key+i, Recoverable = false })
                {
                    using (var reply = await rr.SendRequestAsync(request))
                    {
                        Assert.AreEqual(request.Label, reply.Label);
                        Assert.AreEqual(request.AppSpecific, reply.AppSpecific);
                    }
                }
            }
        }

        private void Server(int count)
        {
            using (var req = new MessageQueue(requestQueueName, QueueAccessMode.Receive))
            {
                req.MessageReadPropertyFilter.AppSpecific = true;
                req.MessageReadPropertyFilter.Label = true;
                using (var res = new MessageQueue(replyQueueName, QueueAccessMode.Send))
                {
                    for (int i = 0; i < count; i++)
                    {
                        using (var serverMsg = req.Receive(TimeSpan.FromSeconds(0.5)))
                        {
                            serverMsg.CorrelationId = serverMsg.Id;
                            res.Send(serverMsg);
                        }
                    }
                }
            }
        }
    }
}
