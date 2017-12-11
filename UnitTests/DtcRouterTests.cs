using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(5000)]
    public class DtcRouterTests
    {
        static string inputQueuePath = $".\\private$\\{nameof(DtcRouterTests)}.Input";
        string outputQueuePath1 = $".\\private$\\{nameof(DtcRouterTests)}.Output.1";
        string outputQueuePath2 = $".\\private$\\{nameof(DtcRouterTests)}.Output.2";
        string inputQueueFormatName;
        string deadQueueFormatName;
        string outputQueueFormatName1;
        string outputQueueFormatName2;
        Queue input;
        Queue dead;
        Queue outRead1;
        Queue outRead2;
        Queue outSend1;
        Queue outSend2;

        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.Transactional);
            outputQueueFormatName1 = Queue.TryCreate(outputQueuePath1, QueueTransactional.Transactional);
            outputQueueFormatName2 = Queue.TryCreate(outputQueuePath2, QueueTransactional.Transactional);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = Queue.Open(inputQueueFormatName, QueueAccessMode.Receive))
                q.Purge();

            outRead1 = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive);
            outRead1.Purge();

            outRead2 = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive);
            outRead2.Purge();

            input = Queue.Open(inputQueueFormatName, QueueAccessMode.Send);

            dead = Queue.Open(deadQueueFormatName, QueueAccessMode.Receive);
            dead.Purge();

            outSend1 = Queue.Open(outputQueueFormatName1, QueueAccessMode.Send);
            outSend2 = Queue.Open(outputQueueFormatName2, QueueAccessMode.Send);
        }

        [Test]
        public async Task can_route_transactional()
        {
            using (var router = new DtcTransactionalRouter(inputQueueFormatName, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "1", AppSpecific = 1 }, QueueTransaction.Single);
                    var got = outRead1.Receive(Properties.All);
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
            using (var router = new DtcTransactionalRouter(inputQueueFormatName, msg => msg.Label.Contains("1") ? outSend1 : outSend2))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "2", AppSpecific = 1 }, QueueTransaction.Single);
                    var got = outRead1.Receive(Properties.All);
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
            using (var router = new DtcTransactionalRouter(inputQueueFormatName, msg => null))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "3", AppSpecific = 1 }, QueueTransaction.Single);
                    var got = dead.Receive(Properties.All);
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
