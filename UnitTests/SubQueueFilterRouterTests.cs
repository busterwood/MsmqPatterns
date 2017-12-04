using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Messaging;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture, Timeout(5000)]
    public class SubQueueFilterRouterTests
    {
        string testQueue = $".\\private$\\{nameof(SubQueueFilterRouterTests)}";

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            if (!MessageQueue.Exists(testQueue))
                MessageQueue.Create(testQueue);
        }

        [SetUp]
        public void Setup()
        {
            if (MessageQueue.Exists(testQueue))
            {
                TestSupport.ReadAllMessages(testQueue);
            }

            if (MessageQueue.Exists(testQueue + ";sq"))
                TestSupport.ReadAllMessages(testQueue + ";sq");
            if (MessageQueue.Exists(testQueue + ";one"))
                TestSupport.ReadAllMessages(testQueue + ";one");
            if (MessageQueue.Exists(testQueue + ";two"))
                TestSupport.ReadAllMessages(testQueue + ";two");
        }

        [Test]
        public async Task can_route_one_message()
        {
            var key = Environment.TickCount;
            var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive);
            using (var router = new SubQueueFilterRouter(q, GetSubQueueName))
            {
                router.StopTime = TimeSpan.FromMilliseconds(20);
                await router.StartAsync();
                try
                {
                    using (var msg = new Message { Label = "my.sq", AppSpecific = key })
                    {
                        q.Send(msg);
                    }
                    using (var sq = new MessageQueue(testQueue + ";sq", QueueAccessMode.Receive))
                    {
                        sq.MessageReadPropertyFilter.AppSpecific = true;
                        using (var got = sq.Receive(TimeSpan.FromMilliseconds(500)))
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
            var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive);
            using (var router = new SubQueueFilterRouter(q, GetSubQueueName))
            {
                router.StopTime = TimeSpan.FromMilliseconds(20);
                await router.StartAsync();
                try
                {
                    using (var msg = new Message { Label = "my.sq", AppSpecific = key })
                    {
                        q.Send(msg);
                    }
                    using (var msg = new Message { Label = "my.sq", AppSpecific = key + 1 })
                    {
                        q.Send(msg);
                    }

                    using (var sq = new MessageQueue(testQueue + ";sq", QueueAccessMode.Receive))
                    {
                        sq.MessageReadPropertyFilter.AppSpecific = true;
                        using (var got = sq.Receive(TimeSpan.FromMilliseconds(500)))
                        {
                            Assert.AreEqual(key, got.AppSpecific);
                        }
                        using (var got = sq.Receive(TimeSpan.FromMilliseconds(500)))
                        {
                            Assert.AreEqual(key + 1, got.AppSpecific);
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
        public async Task can_skip_one_then_route_one_message()
        {
            var key = Environment.TickCount;
            using (var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive))
            {
                var router = new SubQueueFilterRouter(q, GetSubQueueName);
                router.StopTime = TimeSpan.FromMilliseconds(20);
                await router.StartAsync();
                try
                {
                    using (var msg = new Message { Label = "skipped", AppSpecific = key - 1 })
                    {
                        q.Send(msg);
                    }
                    using (var msg = new Message { Label = "my.sq", AppSpecific = key })
                    {
                        q.Send(msg);
                    }
                    using (var sq = new MessageQueue(testQueue + ";sq", QueueAccessMode.Receive))
                    {
                        sq.MessageReadPropertyFilter.AppSpecific = true;
                        using (var got = sq.Receive(TimeSpan.FromMilliseconds(500)))
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
        public async Task can_route_many()
        {
            using (var input = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive))
            using (var out1 = new MessageQueue(testQueue+";one", QueueAccessMode.Receive))
            using (var out2 = new MessageQueue(testQueue+";two", QueueAccessMode.Receive))
            using (var router = new SubQueueFilterRouter(input, GetSubQueueName))
            {
                for (int i = 0; i < 1000; i++)
                {
                    input.Send(new Message { Label = "1", AppSpecific = i });
                    input.Send(new Message { Label = "2", AppSpecific = i });
                }
                router.StopTime = TimeSpan.FromMilliseconds(20);
                var sw = new Stopwatch();
                sw.Start();

                var rtask = router.StartAsync();
                try
                {
                    out1.MessageReadPropertyFilter.AppSpecific = true;
                    out2.MessageReadPropertyFilter.AppSpecific = true;

                    for (int i = 0; i < 1000; i++)
                    {
                        var got = out1.Receive();
                        Assert.AreEqual("1", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                        got = out2.Receive();
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

        static string GetSubQueueName(Message peeked)
        {
            if (peeked.Label.EndsWith("sq", StringComparison.OrdinalIgnoreCase))
                return "sq";
            if (peeked.Label.EndsWith("1", StringComparison.OrdinalIgnoreCase))
                return "one";
            if (peeked.Label.EndsWith("2", StringComparison.OrdinalIgnoreCase))
                return "two";
            return null;
        }
    }

}
