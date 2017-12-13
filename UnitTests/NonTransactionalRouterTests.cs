using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(5000)]
    public class NonTransactionalRouterTests
    {
        static readonly string inputQueuePath = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Input";
        static readonly string adminQueuePath = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Admin";
        static readonly string outputQueuePath1 = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Output.1";
        static readonly string outputQueuePath2 = $".\\private$\\{nameof(NonTransactionalRouterTests)}.Output.2";
        string inputQueueFormatName;
        string adminQueueFormatName;
        string deadQueueFormatName;
        string outputQueueFormatName1;
        string outputQueueFormatName2;
        Queue input;
        Queue dead;
        Queue outRead1;
        Queue outRead2;
        Queue outSend1;
        Queue outSend2;
        Sender sender;


        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.None);
            adminQueueFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);
            outputQueueFormatName1 = Queue.TryCreate(outputQueuePath1, QueueTransactional.None);
            outputQueueFormatName2 = Queue.TryCreate(outputQueuePath2, QueueTransactional.None);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = Queue.Open(inputQueueFormatName, QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(adminQueueFormatName, QueueAccessMode.Receive))
                q.Purge();

            input = Queue.Open(inputQueueFormatName, QueueAccessMode.Send);

            dead = Queue.Open(deadQueueFormatName, QueueAccessMode.Receive);
            dead.Purge();

            outRead1 = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive);
            outRead1.Purge();

            outRead2 = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive);
            outRead2.Purge();

            outSend1 = Queue.Open(outputQueueFormatName1, QueueAccessMode.Send);
            outSend2 = Queue.Open(outputQueueFormatName2, QueueAccessMode.Send);

            sender = new Sender(adminQueueFormatName);
        }

        [Test]
        public async Task can_route_non_transactional()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    await sender.SendAsync(new Message { Label = "1", AppSpecific = 1 }, QueueTransaction.None, input);
                    var got = outRead1.Receive();
                    Assert.AreEqual("1", got.Label);
                }
                finally
                {
                    await router?.StopAsync();
                    await sender.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_non_transactional_to_other_queue()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    await sender.SendAsync(new Message { Label = "2", AppSpecific = 1 }, QueueTransaction.None, input);
                    var got = outRead2.Receive();
                    Assert.AreEqual("2", got.Label);
                }
                finally
                {
                    await router?.StopAsync();
                    await sender.StopAsync();
                }
            }
        }

        [Test]
        public async Task can_route_non_transactional_to_deadletter()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => null))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    await sender.SendAsync(new Message { Label = "3", AppSpecific = 1 }, QueueTransaction.None, input);
                    var got = dead.Receive();
                    Assert.AreEqual("3", got.Label);
                }
                finally
                {
                    await router?.StopAsync();
                    await sender.StopAsync();
                }
            }
        }
    }
}
