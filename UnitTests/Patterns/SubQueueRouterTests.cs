using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(10000)]
    public class SubQueueRouterTests
    {
        string testQueue = $".\\private$\\{nameof(SubQueueRouterTests)}";
        string testQueueFormatName;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            testQueueFormatName = Queues.TryCreate(testQueue, QueueTransactional.None);
        }

        [SetUp]
        public void Setup()
        {
            using (var input = new QueueReader(testQueueFormatName))
            using (var out1 = new QueueReader(testQueueFormatName + ";one"))
            using (var out2 = new QueueReader(testQueueFormatName + ";two"))
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
            using (var router = new SubQueueRouter(testQueueFormatName, GetSubQueue))
            {
                await router.StartAsync();
                try
                {
                    using (var q = new QueueWriter(testQueueFormatName))
                    {
                        var msg = new Message { Label = "my.sq", AppSpecific = key };
                        q.Write(msg);
                    }
                    using (var sq = new QueueReader(testQueueFormatName + ";sq"))
                    {
                        var got = sq.Read(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
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
            using (var router = new SubQueueRouter(testQueueFormatName, GetSubQueue))
            {
                await router.StartAsync();
                try
                {
                    using (var q = new QueueWriter(testQueueFormatName))
                    {
                        q.Write(new Message { Label = "my.sq", AppSpecific = key });
                        q.Write(new Message { Label = "my.sq", AppSpecific = key + 1 });
                    }

                    using (var sq = new QueueReader(testQueueFormatName + ";sq"))
                    {
                        var got = sq.Read(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
                        Assert.AreEqual(key, got.AppSpecific);

                        got = sq.Read(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
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
            using (var router = new SubQueueRouter(testQueueFormatName, GetSubQueue))
            {
                await router.StartAsync();
                try
                {
                    using (var q = new QueueWriter(testQueueFormatName))
                    {
                        q.Write(new Message { Label = "skipped", AppSpecific = key - 1 });
                        q.Write(new Message { Label = "my.sq", AppSpecific = key });
                    }

                    using (var sq = new QueueReader(testQueueFormatName + ";sq"))
                    {
                        var got = sq.Read(Properties.AppSpecific, timeout: TimeSpan.FromMilliseconds(500));
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
            using (var input = new QueueWriter(testQueueFormatName))
            using (var out1 = new QueueReader(testQueueFormatName+";one"))
            using (var out2 = new QueueReader(testQueueFormatName+";two"))
            using (var router = new SubQueueRouter(testQueueFormatName, GetSubQueue))
            {
                out1.Purge();
                out2.Purge();

                for (int i = 0; i < 1000; i++)
                {
                    input.Write(new Message { Label = "1", AppSpecific = i });
                    input.Write(new Message { Label = "2", AppSpecific = i });
                }
                var sw = new Stopwatch();
                sw.Start();

                var rtask = router.StartAsync();
                try
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        var got = out1.Read(Properties.Label | Properties.AppSpecific);
                        Assert.AreEqual("1", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                        got = out2.Read(Properties.Label | Properties.AppSpecific);
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

        QueueCache<SubQueue> _cache = new QueueCache<SubQueue>((fn, access, share) => new SubQueue(fn, share: share));

        SubQueue GetSubQueue(Message peeked)
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
