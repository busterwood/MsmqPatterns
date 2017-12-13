using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(5000), Ignore]
    public class TransactionalRouterTests
    {
        static readonly string inputQueuePath = $".\\private$\\{nameof(TransactionalRouterTests)}.Input";
        static readonly string adminQueuePath = $".\\private$\\{nameof(TransactionalRouterTests)}.Admin";
        static readonly string outputQueuePath1 = $".\\private$\\{nameof(TransactionalRouterTests)}.Output.1";
        static readonly string outputQueuePath2 = $".\\private$\\{nameof(TransactionalRouterTests)}.Output.2";
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
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.Transactional);
            adminQueueFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);
            outputQueueFormatName1 = Queue.TryCreate(outputQueuePath1, QueueTransactional.Transactional);
            outputQueueFormatName2 = Queue.TryCreate(outputQueuePath2, QueueTransactional.Transactional);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = Queue.Open(inputQueueFormatName, QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(inputQueueFormatName+ ";batch", QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive))
                q.Purge();
            using (var q = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive))
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
        public async Task can_route_transactional()
        {
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, sender, Route))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Post(new Message { Label = "1", AppSpecific = 1 }, QueueTransaction.Single);
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
        public async Task can_route_transactional_to_other_queue()
        {
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, sender, Route))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Post(new Message { Label = "2", AppSpecific = 1 }, QueueTransaction.Single);
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
        public async Task can_route_transactional_to_deadletter()
        {
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, sender, Route))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Post(new Message { Label = "3", AppSpecific = 1 }, QueueTransaction.Single);
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

        [Test]
        public async Task can_route_many()
        {
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, sender, Route))
            {
                for (int i = 0; i < 1000; i++)
                {
                    input.Post(new Message { Label = "1", AppSpecific = i }, QueueTransaction.Single);
                    input.Post(new Message { Label = "2", AppSpecific = i }, QueueTransaction.Single);

                }
                var sw = new Stopwatch();
                sw.Start();
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    for (int i = 0; i < 1000; i++)
                    {
                        var got = outRead1.Receive();
                        Assert.AreEqual("1", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                        got = outRead2.Receive();
                        Assert.AreEqual("2", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                    }
                    sw.Stop();
                }
                finally
                {
                    await router?.StopAsync();
                    await sender.StopAsync();
                }
                Console.WriteLine($"Reading 2000 routed messages took {sw.ElapsedMilliseconds:N0} MS");
            }
        }

        Queue Route(Message msg)
        {
            if (msg.Label.Contains("1"))
                return outSend1;
            if (msg.Label.Contains("2"))
                return outSend2;
            return null;
        }
    }
}
