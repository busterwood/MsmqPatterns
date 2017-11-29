using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class TransactionalRouterTests
    {
        string inputQueueName = $".\\private$\\{nameof(TransactionalRouterTests)}.Input";
        string deadQueueName = $".\\private$\\{nameof(TransactionalRouterTests)}.Dead";
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
            if (!MessageQueue.Exists(deadQueueName))
                MessageQueue.Create(deadQueueName, true);
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
            dead = new MessageQueue(inputQueueName, QueueAccessMode.SendAndReceive);
            out1 = new MessageQueue(outputQueueName1, QueueAccessMode.SendAndReceive);
            out2 = new MessageQueue(outputQueueName2, QueueAccessMode.SendAndReceive);
        }

        [Test]
        public async Task can_route_transactional()
        {
            using (var router = Router.New(input, dead, msg => msg.Label.Contains("1") ? out1 : out2))
            {
                router.ReceiveTimeout = TimeSpan.FromMilliseconds(20);
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
            using (var router = Router.New(input, dead, msg => msg.Label.Contains("1") ? out1 : out2))
            {
                router.ReceiveTimeout = TimeSpan.FromMilliseconds(20);
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
            using (var router = Router.New(input, dead, msg => null))
            {
                router.ReceiveTimeout = TimeSpan.FromMilliseconds(20);
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
    }
}
