using BusterWood.Msmq;
using BusterWood.MsmqPatterns;
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
    public class QueueDispatcherTests
    {
        readonly string inputQueuePath = $".\\private$\\{nameof(QueueDispatcherTests)}";
        readonly string adminQueuePath = $".\\private$\\{nameof(QueueDispatcherTests)}.Admin";
        string inputQueueFormatName;
        string adminQueueFormatName;
        QueueWriter inputWriter;
        QueueDispatcher dispatcher;
        Postman postman;

        [SetUp]
        public void Setup()
        {
            inputQueueFormatName = Queue.TryCreate(inputQueuePath, QueueTransactional.None);
            adminQueueFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);

            using (var q = new QueueReader(inputQueueFormatName))
                q.Purge();
            using (var q = new QueueReader(adminQueueFormatName))
                q.Purge();

            inputWriter = new QueueWriter(inputQueueFormatName);

            dispatcher = new QueueDispatcher(inputQueueFormatName);
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
            await postman.DeliverAsync(request, QueueTransaction.None, inputWriter);
            var actual = await tcs.Task;
            Assert.AreEqual(key, actual?.AppSpecific);
            Assert.AreEqual("hello.world", actual?.Label);
            sw.Stop();
            Console.WriteLine($"took {sw.ElapsedMilliseconds:N0}ms");
        }

    }
}
