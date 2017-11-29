using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
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

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            CreateOrClear(requestQueueName);
            CreateOrClear(replyQueueName);
            CreateOrClear(adminQueueName);
        }

        [SetUp]
        public void Setup()
        {
            ReadAllMessages(requestQueueName);
            ReadAllMessages(replyQueueName);
            ReadAllMessages(adminQueueName);
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
                        q.Receive(TimeSpan.FromMilliseconds(10)).Dispose();
                    }
                    catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                    {
                        break;
                    }
                }
            }
        }

        [Test, Timeout(1000)]
        public void can_get_reply()
        {
            var key = Environment.TickCount;
            using (var rr = new RequestReply(requestQueueName, replyQueueName, adminQueueName))
            {
                var sw = new Stopwatch();
                sw.Start();
                using (var request = new Message { Label = "my.sq", AppSpecific = key, Recoverable = false })
                {
                    var serverTask = Task.Run(() => Server(1));
                    using (var reply = rr.SendRequest(request))
                    {
                        Assert.AreEqual(request.Label, reply?.Label);
                        Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                    }
                }
                sw.Stop();
                Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
            }
        }

        [Test, Timeout(1000)]
        public void can_get_reply_via_extension()
        {
            var key = Environment.TickCount;
            var requestQueue = new MessageQueue(requestQueueName, QueueAccessMode.Send);
            var replyQueue = new MessageQueue(replyQueueName, QueueAccessMode.Receive);
            var adminQueue = new MessageQueue(adminQueueName, QueueAccessMode.Receive);

            using (var request = new Message { Label = "my.sq", AppSpecific = key, Recoverable = false })
            {
                var serverTask = Task.Run(() => Server(1));
                using (var reply = requestQueue.SendRequest(request, replyQueue, adminQueue))
                {
                    Assert.AreEqual(request.Label, reply?.Label);
                    Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                }
            }
        }

        [Test, Timeout(1000)]
        public void can_get_multiple_replies()
        {
            var key = Environment.TickCount;
            using (var rr = new RequestReply(requestQueueName, replyQueueName, adminQueueName))
            {
                var serverTask = Task.Run(() => Server(10));

                for (int i = 0; i < 10; i++)
                {
                    using (var request = new Message { Label = "my.sq", AppSpecific = key + i, Recoverable = false })
                    using (var reply = rr.SendRequest(request))
                    {
                        Assert.AreEqual(request.Label, reply?.Label);
                        Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                    }
                }
            }
        }

        [Test, Timeout(1000)]
        public async Task can_get_reply_async()
        {
            var key = Environment.TickCount;
            using (var rr = new RequestReply(requestQueueName, replyQueueName, adminQueueName))
            {
                var sw = new Stopwatch();
                sw.Start();
                using (var request = new Message { Label = "my.sq", AppSpecific = key, Recoverable = false })
                {
                    var serverTask = Task.Run(() => Server(1));
                    using (var reply = await rr.SendRequestAsync(request))
                    {
                        Assert.AreEqual(request.Label, reply?.Label);
                        Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                    }
                }
                sw.Stop();
                Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
            }
        }

        [Test, Timeout(1000)]
        public async Task can_get_multiple_replies_async()
        {
            var key = Environment.TickCount;
            using (var rr = new RequestReply(requestQueueName, replyQueueName, adminQueueName))
            {
                var serverTask = Task.Run(() => Server(10));
                var sw = new Stopwatch();

                for (int i = 0; i < 10; i++)
                {
                    sw.Restart();
                    using (var request = new Message { Label = "my.sq", AppSpecific = key + i, Recoverable = false })
                    using (var reply = await rr.SendRequestAsync(request))
                    {
                        Assert.AreEqual(request.Label, reply?.Label);
                        Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                    }
                    sw.Stop();
                    Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
                }
            }
        }

        private void Server(int count)
        {
            using (var requestQ = new MessageQueue(requestQueueName, QueueAccessMode.Receive))
            {
                requestQ.MessageReadPropertyFilter.AppSpecific = true;
                requestQ.MessageReadPropertyFilter.Label = true;
                using (var res = new MessageQueue(replyQueueName, QueueAccessMode.Send))
                {
                    for (int i = 0; i < count; i++)
                    {
                        using (var request = requestQ.Receive(TimeSpan.FromSeconds(0.5)))
                        {
                            request.CorrelationId = request.Id;
                            res.Send(request);
                        }
                    }
                }
            }
        }
    }
}
