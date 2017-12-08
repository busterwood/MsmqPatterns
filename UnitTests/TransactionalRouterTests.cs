using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Messaging;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture, Ignore]
    public class TransactionalRouterTests
    {
        static string inputQueueName = $".\\private$\\{nameof(TransactionalRouterTests)}.Input";
        string deadQueueName = $"{inputQueueName};Poison";
        string outputQueueName1 = $".\\private$\\{nameof(TransactionalRouterTests)}.Output.1";
        string outputQueueName2 = $".\\private$\\{nameof(TransactionalRouterTests)}.Output.2";
        MessageQueue input;
        MessageQueue dead;
        MessageQueue out1;
        MessageQueue out2;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            if (!MessageQueue.Exists(inputQueueName))
                MessageQueue.Create(inputQueueName, true);
            if (!MessageQueue.Exists(outputQueueName1))
                MessageQueue.Create(outputQueueName1, true);
            if (!MessageQueue.Exists(outputQueueName2))
                MessageQueue.Create(outputQueueName2, true);
        }

        [SetUp]
        public void Setup()
        {
            if (MessageQueue.Exists(inputQueueName))
                TestSupport.ReadAllMessages(inputQueueName);
            if (MessageQueue.Exists(deadQueueName))
                TestSupport.ReadAllMessages(deadQueueName);
            if (MessageQueue.Exists(outputQueueName1))
                TestSupport.ReadAllMessages(outputQueueName1);
            if (MessageQueue.Exists(outputQueueName2))
                TestSupport.ReadAllMessages(outputQueueName2);

            input = new MessageQueue(inputQueueName, QueueAccessMode.SendAndReceive);
            dead = new MessageQueue(deadQueueName, QueueAccessMode.SendAndReceive);
            out1 = new MessageQueue(outputQueueName1, QueueAccessMode.SendAndReceive);
            out2 = new MessageQueue(outputQueueName2, QueueAccessMode.SendAndReceive);
            input.MessageReadPropertyFilter.AppSpecific = true;
            out1.MessageReadPropertyFilter.AppSpecific = true;
            out2.MessageReadPropertyFilter.AppSpecific = true;
        }

        [Test]
        public async Task can_route_transactional()
        {
            using (var router = Router.New(input, Route))
            {
                router.StopTime = TimeSpan.FromMilliseconds(20);
                var rtask = router.StartAsync();
                try
                {
                    input.Send(new Message { Label = "1", AppSpecific = 1 }, MessageQueueTransactionType.Single);
                    var got = out1.Receive();
                    Assert.AreEqual("1", got.Label);
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_transactional_to_other_queue()
        {
            using (var router = Router.New(input, Route))
            {
                router.StopTime = TimeSpan.FromMilliseconds(20);
                var rtask = router.StartAsync();
                try
                {
                    input.Send(new Message { Label = "2", AppSpecific = 1 }, MessageQueueTransactionType.Single);
                    var got = out2.Receive();
                    Assert.AreEqual("2", got.Label);
                }
                finally
                {
                    await router.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_transactional_to_deadletter()
        {
            using (var router = Router.New(input, Route))
            {
                router.StopTime = TimeSpan.FromMilliseconds(20);
                var rtask = router.StartAsync();
                try
                {
                    input.Send(new Message { Label = "3", AppSpecific = 1 }, MessageQueueTransactionType.Single);
                    var got = dead.Receive();
                    Assert.AreEqual("3", got.Label);
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
            using (var router = new MsmqTransactionalRouter(input, Route))
            {
                for (int i = 0; i < 1000; i++)
                {
                    input.Send(new Message { Label = "1", AppSpecific = i }, MessageQueueTransactionType.Single);
                    input.Send(new Message { Label = "2", AppSpecific = i }, MessageQueueTransactionType.Single);

                }
                router.StopTime = TimeSpan.FromMilliseconds(20);
                var sw = new Stopwatch();
                sw.Start();
                var rtask = router.StartAsync();
                try
                {
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

        MessageQueue Route(Message msg)
        {
            if (msg.Label.Contains("1"))
                return out1;
            if (msg.Label.Contains("2"))
                return out2;
            return null;
        }
    }
}
