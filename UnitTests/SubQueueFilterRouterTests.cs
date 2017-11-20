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
                ReadAllMessages(testQueue);
            }

            if (MessageQueue.Exists(testQueue + ";sq"))
                ReadAllMessages(testQueue + ";sq");
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

        [Test]
        public async Task can_route_one_message()
        {
            var key = Environment.TickCount;
            var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive);
            var fr = new SubQueueFilterRouter(q, GetSubQueueName);
            await fr.StartAsync();
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
                await fr.StopAsync();
            }
        }

        [Test]
        public async Task can_route_multiple_message()
        {
            var key = Environment.TickCount;
            var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive);
            var fr = new SubQueueFilterRouter(q, GetSubQueueName);
            await fr.StartAsync();
            try
            {
                using (var msg = new Message { Label = "my.sq", AppSpecific = key })
                {
                    q.Send(msg);
                }
                using (var msg = new Message { Label = "my.sq", AppSpecific = key+1 })
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
                        Assert.AreEqual(key+1, got.AppSpecific);
                    }
                }
            }
            finally
            {
                await fr.StopAsync();
            }
        }

        [Test]
        public async Task can_skip_one_then_route_one_message()
        {
            var key = Environment.TickCount;
            var q = new MessageQueue(testQueue, QueueAccessMode.SendAndReceive);
            var fr = new SubQueueFilterRouter(q, GetSubQueueName);
            await fr.StartAsync();
            try
            {
                using (var msg = new Message { Label = "skipped", AppSpecific = key-1 })
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
                await fr.StopAsync();
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
