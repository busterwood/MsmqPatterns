using BusterWood.Msmq;
using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class LabelSubscriberTests
    {
        readonly string inputQueuePath = $".\\private$\\{nameof(LabelSubscriberTests)}";
        readonly string adminQueuePath = $".\\private$\\{nameof(LabelSubscriberTests)}.Admin";
        string inputQueueFormatName;
        string adminQueueFormatName;
        QueueWriter inputWriter;
        LabelSubscriber dispatcher;
        Postman postman;

        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queues.TryCreate(inputQueuePath, QueueTransactional.None);
            adminQueueFormatName = Queues.TryCreate(adminQueuePath, QueueTransactional.None);

            using (var q = new QueueReader(inputQueueFormatName))
                q.Purge();
            using (var q = new QueueReader(adminQueueFormatName))
                q.Purge();

            inputWriter = new QueueWriter(inputQueueFormatName);

            dispatcher = new LabelSubscriber(inputQueueFormatName);
            dispatcher.StartAsync();

            postman = new Postman(adminQueueFormatName);
            postman.StartAsync();
        }

        [TearDown]
        public void TearDown()
        {
            dispatcher.Dispose();
            postman.Dispose();
            inputWriter.Dispose();
        }

        [Test, Timeout(1000)]
        public async Task can_dispatch_message_matching_subscription()
        {
            var tcs = new TaskCompletionSource<Message>();
            dispatcher.Subscribe("hello.world", msg => tcs.SetResult(msg));
            var key = Environment.TickCount;
            var request = new Message { Label = "hello.world", AppSpecific = key };

            var sw = new Stopwatch();
            sw.Start();
            await inputWriter.DeliverAsync(request, postman, QueueTransaction.None);
            var actual = await tcs.Task;
            Assert.AreEqual(key, actual?.AppSpecific);
            Assert.AreEqual("hello.world", actual?.Label);
            sw.Stop();
            Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
        }

    }
}
