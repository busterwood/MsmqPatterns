using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BusterWood.Msmq;

namespace UnitTests
{
    [TestFixture, Ignore]
    public class TransactionalRouterTests
    {
        static string inputQueuePath = $".\\private$\\{nameof(TransactionalRouterTests)}.Input";
        string outputQueuePath1 = $".\\private$\\{nameof(TransactionalRouterTests)}.Output.1";
        string outputQueuePath2 = $".\\private$\\{nameof(TransactionalRouterTests)}.Output.2";
        string inputQueueFormatName;
        string deadQueueFormatName;
        string outputQueueFormatName1;
        string outputQueueFormatName2;
        Queue input;
        Queue dead;
        Queue out1;
        Queue out2;

        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.Transactional);
            outputQueueFormatName1 = Queue.TryCreate(outputQueuePath1, QueueTransactional.Transactional);
            outputQueueFormatName2 = Queue.TryCreate(outputQueuePath2, QueueTransactional.Transactional);
            deadQueueFormatName = $"{inputQueueFormatName };Poison";

            using (var q = Queue.Open(inputQueueFormatName, QueueAccessMode.Receive))
                q.Purge();

            using (var q = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive))
                q.Purge();

            using (var q = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive))
                q.Purge();

            input = Queue.Open(inputQueueFormatName, QueueAccessMode.Send);
            dead = Queue.Open(deadQueueFormatName, QueueAccessMode.Receive);
            out1 = Queue.Open(outputQueueFormatName1, QueueAccessMode.Receive);
            out2 = Queue.Open(outputQueueFormatName2, QueueAccessMode.Receive);
        }

        [Test]
        public async Task can_route_transactional()
        {
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, Route))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "1", AppSpecific = 1 }, QueueTransaction.Single);
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
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, Route))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "2", AppSpecific = 1 }, QueueTransaction.Single);
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
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, Route))
            {
                var rtask = router.StartAsync();
                try
                {
                    input.Post(new Message { Label = "3", AppSpecific = 1 }, QueueTransaction.Single);
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
            using (var router = new MsmqTransactionalRouter(inputQueueFormatName, Route))
            {
                for (int i = 0; i < 1000; i++)
                {
                    input.Post(new Message { Label = "1", AppSpecific = i }, QueueTransaction.Single);
                    input.Post(new Message { Label = "2", AppSpecific = i }, QueueTransaction.Single);

                }
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

        Queue Route(Message msg)
        {
            if (msg.Label.Contains("1"))
                return out1;
            if (msg.Label.Contains("2"))
                return out2;
            return null;
        }
    }
}
