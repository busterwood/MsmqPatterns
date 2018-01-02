using BusterWood.Msmq;
using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UnitTests.Cache
{
    [TestFixture, Timeout(1000)]
    public class MessageCacheTests
    {
        static string CacheInputPath = $".\\private$\\{nameof(MessageCacheTests)}.input";
        static string CacheAdminPath = $".\\private$\\{nameof(MessageCacheTests)}.input.admin";
        static readonly string ReplyPath = $".\\private$\\{nameof(MessageCacheTests)}.reply";
        static readonly string ReplyAdminPath = $".\\private$\\{nameof(MessageCacheTests)}.reply.admin";
        string cacheInputFormatName;
        string cacheAdminFormatName;
        string replyFormatName;
        string replyAdminFormatName;
        Postman postman;
        MessageCache cache;

        [SetUp]
        public void Setup()
        {
            cacheInputFormatName = Queues.TryCreate(CacheInputPath, QueueTransactional.None);
            cacheAdminFormatName = Queues.TryCreate(CacheAdminPath, QueueTransactional.None);
            replyFormatName = Queues.TryCreate(ReplyPath, QueueTransactional.None);
            replyAdminFormatName = Queues.TryCreate(ReplyAdminPath, QueueTransactional.None);

            using (var q = new QueueReader(cacheInputFormatName))
                q.Purge();
            using (var q = new QueueReader(cacheAdminFormatName))
                q.Purge();
            using (var q = new QueueReader(replyFormatName))
                q.Purge();
            using (var q = new QueueReader(replyAdminFormatName))
                q.Purge();

            postman = new Postman(replyAdminFormatName);
            postman.StartAsync().Wait();
            cache = new MessageCache(cacheInputFormatName, cacheAdminFormatName, null, TimeSpan.FromDays(1));
            cache.StartAsync().Wait();
        }

        [TearDown]
        public void Stop()
        {
            cache.Dispose();
            postman.Dispose();
        }

        [Test]
        public async Task cache_returns_empty_message_when_label_not_in_cache()
        {
            using (var rr = new RequestReply(cacheInputFormatName, replyFormatName, postman))
            {
                var msg = new Message { Label = "last.some.value" };
                var sw = new Stopwatch();
                sw.Start();
                var reply = await rr.SendRequestAsync(msg);
                Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}MS");
                Assert.AreEqual(null, reply.Body, "Body");
                Assert.AreEqual("some.value", reply.Label, "Label");
            }
        }

        [Test]
        public async Task cache_returns_message_stored_for_label()
        {
            using (var inputWriter = new QueueWriter(cacheInputFormatName))
            using (var rr = new RequestReply(cacheInputFormatName, replyFormatName, postman))
            {
                var cachedMsg = new Message { Label = "some.value", AppSpecific = 234 };
                cachedMsg.BodyASCII("hello world!");
                await postman.DeliverAsync(cachedMsg, inputWriter, QueueTransaction.None);

                var request = new Message { Label = "last.some.value" };
                var sw = new Stopwatch();
                sw.Start();
                var reply = await rr.SendRequestAsync(request);
                Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}MS");
                Assert.AreEqual(cachedMsg.Label, reply.Label, "Label");
                Assert.AreEqual(cachedMsg.AppSpecific, reply.AppSpecific, "AppSpecific");
                Assert.AreEqual("hello world!", reply.BodyASCII(), "Body");
            }
        }

        [Test]
        public async Task cache_many_requests()
        {
            using (var inputWriter = new QueueWriter(cacheInputFormatName))
            using (var rr = new RequestReply(cacheInputFormatName, replyFormatName, postman))
            {
                var cachedMsg = new Message { Label = "some.value.1", AppSpecific = 1 };
                await postman.DeliverAsync(cachedMsg, inputWriter, QueueTransaction.None);

                var cachedMsg2 = new Message { Label = "some.value.2", AppSpecific = 2 };
                await postman.DeliverAsync(cachedMsg2, inputWriter, QueueTransaction.None);

                var sw = new Stopwatch();
                for (int j = 0; j < 5; j++)
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        sw.Restart();
                        var request = new Message { Label = "last.some.value." + i };
                        var reply = await rr.SendRequestAsync(request);
                        Console.WriteLine($"took {sw.Elapsed.TotalMilliseconds:N1}MS");
                        Assert.AreEqual("some.value." + i, reply.Label, "Label");
                        Assert.AreEqual(i, reply.AppSpecific, "AppSpecific");
                    }
                }
            }
        }
    }
}
