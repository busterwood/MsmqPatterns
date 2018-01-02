using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Timeout(5000)]
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
            inputQueueFormatName = Queues.TryCreate(inputQueuePath, QueueTransactional.Transactional);
            adminQueueFormatName = Queues.TryCreate(adminQueuePath, QueueTransactional.None);
            outputQueueFormatName1 = Queues.TryCreate(outputQueuePath1, QueueTransactional.Transactional);
            outputQueueFormatName2 = Queues.TryCreate(outputQueuePath2, QueueTransactional.Transactional);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = new QueueReader(inputQueueFormatName))
                q.Purge();
            using (var q = new QueueReader(inputQueueFormatName+ ";batch"))
                q.Purge();
            using (var q = new QueueReader(outputQueueFormatName1))
                q.Purge();
            using (var q = new QueueReader(outputQueueFormatName2))
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
        public void TearDown()
        {
            sender.Dispose();
            input.Dispose();
            dead.Dispose();
            outRead1.Dispose();
            outSend1.Dispose();
            outRead2.Dispose();
            outSend2.Dispose();
        }

        [Test]
        public async Task can_route_transactional()
        {
            using (var router = new TransactionalRouter(inputQueueFormatName, sender, Route))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Write(new Message { Label = "1", AppSpecific = 1 }, QueueTransaction.Single);
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
        public async Task can_route_transactional_to_other_queue()
        {
            using (var router = new TransactionalRouter(inputQueueFormatName, sender, Route))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Write(new Message { Label = "2", AppSpecific = 1 }, QueueTransaction.Single);
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
        public async Task can_route_transactional_to_deadletter()
        {
            using (var router = new TransactionalRouter(inputQueueFormatName, sender, Route))
            {
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    input.Write(new Message { Label = "3", AppSpecific = 1 }, QueueTransaction.Single);
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

        [Test, Timeout(10000)]//, Ignore("too slow to run every time")]
        public async Task can_route_many()
        {
            using (var router = new TransactionalRouter(inputQueueFormatName, sender, Route))
            {
                for (int i = 0; i < 1000; i++)
                {
                    input.Write(new Message { Label = "1", AppSpecific = i }, QueueTransaction.Single);
                    input.Write(new Message { Label = "2", AppSpecific = i }, QueueTransaction.Single);

                }
                var sw = new Stopwatch();
                sw.Start();
                await sender.StartAsync();
                try
                {
                    var rtask = router.StartAsync();
                    for (int i = 0; i < 1000; i++)
                    {
                        var got = outRead1.Read();
                        Assert.AreEqual("1", got.Label, "Label");
                        Assert.AreEqual(i, got.AppSpecific, "AppSpecific");
                        got = outRead2.Read();
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

        QueueWriter Route(Message msg)
        {
            if (msg.Label.Contains("1"))
                return outSend1;
            if (msg.Label.Contains("2"))
                return outSend2;
            return null;
        }
    }
}
