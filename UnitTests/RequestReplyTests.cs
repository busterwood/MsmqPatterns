using BusterWood.MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class RequestReplyTests
    {
        readonly string requestQueuePath = $".\\private$\\{nameof(RequestReplyTests)}.Request";
        readonly string replyQueuePath = $".\\private$\\{nameof(RequestReplyTests)}.Reply";
        readonly string adminQueuePath = $".\\private$\\{nameof(RequestReplyTests)}.Admin";
        string requestQueueFormatName;
        string replyQueueFormatName;
        string adminQueueFormatName;
        Postman postman;

        [SetUp]
        public void Setup()
        {
            requestQueueFormatName = Queue.TryCreate(requestQueuePath, QueueTransactional.None);
            replyQueueFormatName = Queue.TryCreate(replyQueuePath, QueueTransactional.None);
            adminQueueFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);

            using (var q = new QueueReader(requestQueueFormatName))
                q.Purge();
            using (var q = new QueueReader(replyQueueFormatName))
                q.Purge();
            using (var q = new QueueReader(adminQueueFormatName))
                q.Purge();

            postman = new Postman(adminQueueFormatName);
            postman.StartAsync();
        }

        [TearDown]
        public void TearDown()
        {
            postman.Dispose();
        }

        [Test, Timeout(1000)]
        public void can_get_reply()
        {
            var key = Environment.TickCount;
            var serverTask = Task.Run(() => Server(1));
            using (var rr = new RequestReply(requestQueueFormatName, replyQueueFormatName, postman))
            {
                var sw = new Stopwatch();
                sw.Start();
                var request = new Message { Label = "my.sq", AppSpecific = key };
                var reply = rr.SendRequest(request);
                Assert.AreEqual(request.Label, reply?.Label);
                Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                sw.Stop();
                Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
            }
        }

        [Test, Timeout(1000)]
        public void can_get_multiple_replies()
        {
            var key = Environment.TickCount;
            var serverTask = Task.Run(() => Server(10));
            using (var rr = new RequestReply(requestQueueFormatName, replyQueueFormatName, postman))
            {
                var sw = new Stopwatch();
                for (int i = 0; i < 10; i++)
                {
                    sw.Restart();
                    var request = new Message { Label = "my.sq", AppSpecific = key + i };
                    var reply = rr.SendRequest(request);
                    Assert.AreEqual(request.Label, reply?.Label);
                    Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                    sw.Stop();
                    Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
                    key++;
                }
            }
        }

        [Test, Timeout(1000)]
        public async Task can_get_reply_async()
        {
            var key = Environment.TickCount;
            var serverTask = Task.Run(() => Server(1));
            using (var rr = new RequestReply(requestQueueFormatName, replyQueueFormatName, postman))
            {
                var sw = new Stopwatch();
                sw.Start();
                var request = new Message { Label = "my.sq", AppSpecific = key };
                var reply = await rr.SendRequestAsync(request);
                Assert.AreEqual(request.Label, reply?.Label);
                Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                sw.Stop();
                Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
            }
        }

        [Test, Timeout(1000)]
        public async Task can_get_multiple_replies_async()
        {
            var key = Environment.TickCount;
            var serverTask = Task.Run(() => Server(10));
            using (var rr = new RequestReply(requestQueueFormatName, replyQueueFormatName, postman))
            {
                var sw = new Stopwatch();
                for (int i = 0; i < 10; i++)
                {
                    sw.Restart();
                    var request = new Message { Label = "my.sq", AppSpecific = key + i };
                    var reply = await rr.SendRequestAsync(request);
                    Assert.AreEqual(request.Label, reply?.Label);
                    Assert.AreEqual(request.AppSpecific, reply?.AppSpecific);
                    sw.Stop();
                    Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
                    key++;
                }
            }
        }

        private void Server(int count)
        {
            using (var requestQ = new QueueReader(requestQueueFormatName))
            using (var replyQ = new QueueWriter(replyQueueFormatName))
            {
                for (int i = 0; i < count; i++)
                {
                    var msg = requestQ.Read(Properties.All, TimeSpan.FromSeconds(0.5));
                    msg.CorrelationId = msg.Id;
                    replyQ.Write(msg);
                }
            }
        }
    }
}
