using BusterWood.MsmqPatterns;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(5000), Ignore("till the router uses the admin queue")]
    public class DtcRouterTests
    {
        static readonly string inputQueuePath = $".\\private$\\{nameof(DtcRouterTests)}.Input";
        static readonly string adminQueuePath = $".\\private$\\{nameof(DtcRouterTests)}.Admin";
        static readonly string outputQueuePath1 = $".\\private$\\{nameof(DtcRouterTests)}.Output.1";
        static readonly string outputQueuePath2 = $".\\private$\\{nameof(DtcRouterTests)}.Output.2";
        string inputQueueFormatName;
        string adminQueueFormatName;
        string deadQueueFormatName;
        string outputQueueFormatName1;
        string outputQueueFormatName2;
        QueueWriter input;
        //Queue admin;
        QueueReader dead;
        QueueReader outRead1;
        QueueReader outRead2;
        QueueWriter outSend1;
        QueueWriter outSend2;
        Postman sender;

        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.Transactional);
            adminQueueFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);
            outputQueueFormatName1 = Queue.TryCreate(outputQueuePath1, QueueTransactional.Transactional);
            outputQueueFormatName2 = Queue.TryCreate(outputQueuePath2, QueueTransactional.Transactional);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = new QueueReader(inputQueueFormatName))
                q.Purge();

            using (var q = new QueueReader(adminQueueFormatName))
                q.Purge();

            
            input = new QueueWriter(inputQueueFormatName);
            dead = new QueueReader(deadQueueFormatName, QueueAccessMode.Receive);
            dead.Purge();

            outRead1 = new QueueReader(outputQueueFormatName1);
            outRead1.Purge();

            outRead2 = new QueueReader(outputQueueFormatName2);
            outRead2.Purge();

            outSend1 = new QueueWriter(outputQueueFormatName1);
            outSend2 = new QueueWriter(outputQueueFormatName2);

            sender = new Postman(adminQueueFormatName);
        }

        [Test]
        public async Task can_route_transactional()
        {
            using (var router = new DtcTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Write(new Message { Label = "1", AppSpecific = 1 }, QueueTransaction.Single);
                    var got = outRead1.Read(Properties.All);
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
            using (var router = new DtcTransactionalRouter(inputQueueFormatName, sender, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Write(new Message { Label = "2", AppSpecific = 1 }, QueueTransaction.Single);
                    var got = outRead1.Read(Properties.All);
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
            using (var router = new DtcTransactionalRouter(inputQueueFormatName, sender, msg => null))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Write(new Message { Label = "3", AppSpecific = 1 }, QueueTransaction.Single);
                    var got = dead.Read(Properties.All);
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
