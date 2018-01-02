using BusterWood.Msmq.Patterns;
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
        QueueWriter input;
        QueueReader dead;
        QueueReader outRead1;
        QueueReader outRead2;
        QueueWriter outSend1;
        QueueWriter outSend2;
        Postman sender;


        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queues.TryCreate(inputQueuePath, QueueTransactional.None);
            adminQueueFormatName = Queues.TryCreate(adminQueuePath, QueueTransactional.None);
            outputQueueFormatName1 = Queues.TryCreate(outputQueuePath1, QueueTransactional.None);
            outputQueueFormatName2 = Queues.TryCreate(outputQueuePath2, QueueTransactional.None);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = new QueueReader(inputQueueFormatName))
                q.Purge();
            using (var q = new QueueReader(adminQueueFormatName))
                q.Purge();

            input = new QueueWriter(inputQueueFormatName);

            dead = new QueueReader(deadQueueFormatName);
            dead.Purge();

            outRead1 = new QueueReader(outputQueueFormatName1);
            outRead1.Purge();

            outRead2 = new QueueReader(outputQueueFormatName2);
            outRead2.Purge();

            outSend1 = new QueueWriter(outputQueueFormatName1);
            outSend2 = new QueueWriter(outputQueueFormatName2);

            sender = new Postman(adminQueueFormatName);
        }

        [TearDown]


        [Test]
        public async Task can_route_non_transactional()
        {
            using (var router = new NonTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Write(new Message { Label = "1", AppSpecific = 1 });
                    var got = outRead1.Read();
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
                    input.Write(new Message { Label = "2", AppSpecific = 1 });
                    var got = outRead2.Read();
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
                    input.Write(new Message { Label = "3", AppSpecific = 1 });
                    var got = dead.Read();
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
