using BusterWood.Msmq;
using BusterWood.Msmq.Cache;
using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
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
            cacheInputFormatName = Queue.TryCreate(CacheInputPath, QueueTransactional.None);
            cacheAdminFormatName = Queue.TryCreate(CacheAdminPath, QueueTransactional.None);
            replyFormatName = Queue.TryCreate(ReplyPath, QueueTransactional.None);
            replyAdminFormatName = Queue.TryCreate(ReplyAdminPath, QueueTransactional.None);

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
                var reply = await rr.SendRequestAsync(msg);
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
                await postman.DeliverAsync(cachedMsg, QueueTransaction.None, inputWriter);

                var request = new Message { Label = "last.some.value" };
                var reply = await rr.SendRequestAsync(request);
                Assert.AreEqual(cachedMsg.Label, reply.Label, "Label");
                Assert.AreEqual(cachedMsg.AppSpecific, reply.AppSpecific, "AppSpecific");
                Assert.AreEqual("hello world!", reply.BodyASCII(), "Body");
            }
        }
    }
}
