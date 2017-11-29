using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Messaging;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture, Timeout(1000)]
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
        }


        [Test]
        public async Task can_route_one_message()
        {
            var key = Environment.TickCount;
            var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive);
            using (var router = new SubQueueFilterRouter(q, GetSubQueueName))
            {
                router.ReceiveTimeout = TimeSpan.FromMilliseconds(20);
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
                router.ReceiveTimeout = TimeSpan.FromMilliseconds(20);
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
                router.ReceiveTimeout = TimeSpan.FromMilliseconds(20);
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

        static string GetSubQueueName(Message peeked)
        {
            if (peeked.Label.EndsWith("sq", StringComparison.OrdinalIgnoreCase))
                return "sq";

            return null;
        }
    }

}
