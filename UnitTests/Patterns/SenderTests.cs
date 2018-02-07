using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture, Timeout(1000)]
    public class SenderTests
    {
        static string destQueuePath = $".\\private$\\{nameof(SenderTests)}.Input";
        string adminQueuePath = $".\\private$\\{nameof(SenderTests)}.Admin";
        QueueWriter dest;
        QueueReader admin;
        string destFormatName;
        string adminFormatName;

        [SetUp]
        public void Setup()
        {
            destFormatName = Queues.TryCreate(destQueuePath, QueueTransactional.Transactional);
            adminFormatName = Queues.TryCreate(adminQueuePath, QueueTransactional.None);
            using (var purgeDest = new QueueReader(destFormatName))
            {
                purgeDest.Purge();
            }
            dest = new QueueWriter(destFormatName);
            admin = new QueueReader(adminFormatName);
            admin.Purge();
        }

        [TearDown]
        public void TearDown()
        {
            dest.Dispose();
            admin.Dispose();
        }

        [Test]
        public async Task send_completes_when_delivered_to_queue()
        {
            using (var sender = new Postman(adminFormatName))
            {
                await sender.StartAsync();
                var msg = new Message { Label = "send1" };
                await dest.DeliverAsync(msg, sender, QueueTransaction.Single);
            }
        }

        [Test]
        public async Task send_throw_exception_when_sending_non_transactional_message_to_transactional_queue()
        {
            using (var sender = new Postman(adminFormatName))
            {
                await sender.StartAsync();
                var msg = new Message { Label = "send2" };
                try
                {
                    await dest.DeliverAsync(msg, sender, QueueTransaction.None);
                    Assert.Fail("Exception not thrown");
                }
                catch (AcknowledgmentException ex)
                {
                    Assert.AreEqual(MessageClass.NotTransactionalMessage, ex.Acknowledgment);
                }
            }
        }

        [Test]
        public async Task send_throw_exception_when_destination_machine_does_not_exist()
        {
            using (var doesNotExist = new QueueWriter("Direct=OS:mini-mac\\private$\\some-queue"))
            using (var sender = new Postman(adminFormatName))
            {
                sender.ReachQueueTimeout = TimeSpan.FromMilliseconds(100);
                await sender.StartAsync();
                var msg = new Message { Label = "send3" };
                try
                {
                    await doesNotExist.DeliverAsync(msg, sender, QueueTransaction.Single);
                    Assert.Fail("Exception not thrown");
                }
                catch (AcknowledgmentException ex)
                {
                    Assert.AreEqual(MessageClass.ReachQueueTimeout, ex.Acknowledgment);
                }
            }
        }

    }
}
