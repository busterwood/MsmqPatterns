using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(10000)]
    public class SubQueueFilterRouterTests
    {
        string testQueue = $".\\private$\\{nameof(SubQueueFilterRouterTests)}";
        string testQueueFormatName;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            testQueueFormatName = Queue.TryCreate(testQueue, QueueTransactional.None);
        }

        [SetUp]
        public void Setup()
        {
            using (var input = Queue.Open(testQueueFormatName, QueueAccessMode.Receive))
            using (var out1 = Queue.Open(testQueueFormatName + ";one", QueueAccessMode.Receive))
            using (var out2 = Queue.Open(testQueueFormatName + ";two", QueueAccessMode.Receive))
            {
                input.Purge();
                out1.Purge();
                out2.Purge();
            }
        }

        [Test]
        public async Task can_route_one_message()
        {
            var key = Environment.TickCount;
            using (var router = new SubQueueFilterRouter(testQueueFormatName, GetSubQueue))
            {
                await router.StartAsync();
                try
                {
                    using (var q = Queue.Open(testQueueFormatName, QueueAccessMode.Send))
                    {
                        var msg = new Message { Label = "my.sq", AppSpecific = key };
                        q.Post(msg);
                    }
                    using (var sq = Queue.Open(testQueueFormatName + ";sq", QueueAccessMode.Receive))
                    {
                        var got = sq.Receive(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
                        {
                            Assert.AreEqual(key, got.AppSpecific);
                        }
                    }
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_multiple_message()
        {
            var key = Environment.TickCount;
            using (var router = new SubQueueFilterRouter(testQueueFormatName, GetSubQueue))
            {
                await router.StartAsync();
                try
                {
                    using (var q = Queue.Open(testQueueFormatName, QueueAccessMode.Send))
                    {
                        q.Post(new Message { Label = "my.sq", AppSpecific = key });
                        q.Post(new Message { Label = "my.sq", AppSpecific = key + 1 });
                    }

                    using (var sq = Queue.Open(testQueueFormatName + ";sq", QueueAccessMode.Receive))
                    {
                        var got = sq.Receive(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
                        Assert.AreEqual(key, got.AppSpecific);

                        got = sq.Receive(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
                        Assert.AreEqual(key + 1, got.AppSpecific);                        
                    }
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_skip_one_then_route_one_message()
        {
            var key = Environment.TickCount;
            using (var router = new SubQueueFilterRouter(testQueueFormatName, GetSubQueue))
            {
                await router.StartAsync();
                try
                {
                    using (var q = Queue.Open(testQueueFormatName, QueueAccessMode.Send))
                    {
                        q.Post(new Message { Label = "skipped", AppSpecific = key - 1 });
                        q.Post(new Message { Label = "my.sq", AppSpecific = key });
                    }

                    using (var sq = Queue.Open(testQueueFormatName + ";sq", QueueAccessMode.Receive))
                    {
                        var got = sq.Receive(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
                        Assert.AreEqual(key, got.AppSpecific);
                    }
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_many()
        {
            using (var input = Queue.Open(testQueueFormatName, QueueAccessMode.Send))
            using (var out1 = Queue.Open(testQueueFormatName+";one", QueueAccessMode.Receive))
            using (var out2 = Queue.Open(testQueueFormatName+";two", QueueAccessMode.Receive))
            using (var router = new SubQueueFilterRouter(testQueueFormatName, GetSubQueue))
            {
                out1.Purge();
                out2.Purge();

                for (int i = 0; i < 1000; i++)
                {
                    input.Post(new Message { Label = "1", AppSpecific = i });
                    input.Post(new Message { Label = "2", AppSpecific = i });
                }
                var sw = new Stopwatch();
                sw.Start();

                var rtask = router.StartAsync();
                try
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        var got = out1.Receive(Properties.Label | Properties.AppSpecific);
                        Assert.AreEqual("1", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                        got = out2.Receive(Properties.Label | Properties.AppSpecific);
                        Assert.AreEqual("2", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                    }
                    sw.Stop();
                }
                finally
                {
                    await router.StopAsync();
                }

                Console.WriteLine($"Reading 2000 routed messages took {sw.ElapsedMilliseconds:N0} MS");
            }
        }

        QueueCache _cache = new QueueCache();

        Queue GetSubQueue(Message peeked)
        {
            if (peeked.Label.EndsWith("sq", StringComparison.OrdinalIgnoreCase))
                return _cache.Open(testQueueFormatName + ";sq", QueueAccessMode.Move);
            if (peeked.Label.EndsWith("1", StringComparison.OrdinalIgnoreCase))
                return _cache.Open(testQueueFormatName + ";one", QueueAccessMode.Move);
            if (peeked.Label.EndsWith("2", StringComparison.OrdinalIgnoreCase))
                return _cache.Open(testQueueFormatName + ";two", QueueAccessMode.Move);
            return null;
        }
    }

}
