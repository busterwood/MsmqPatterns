using MsmqPatterns;
using NUnit.Framework;
using System;
using BusterWood.Msmq;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture, Timeout(1000), Ignore]
    public class SenderTests
    {
        static string destQueuePath = $".\\private$\\{nameof(SenderTests)}.Input";
        string adminQueuePath = $".\\private$\\{nameof(SenderTests)}.Admin";
        Queue dest;
        Queue admin;
        string destFormatName;
        string adminFormatName;

        [SetUp]
        public void Setup()
        {
            destFormatName = Queue.TryCreate(destQueuePath, QueueTransactional.Transactional);
            adminFormatName = Queue.TryCreate(adminQueuePath, QueueTransactional.None);
            using (var purgeDest = Queue.Open(destFormatName, QueueAccessMode.Receive))
            {
                purgeDest.Purge();
            }
            dest = Queue.Open(destFormatName, QueueAccessMode.Send);
            admin = Queue.Open(adminFormatName, QueueAccessMode.Receive);
            admin.Purge();
        }

        [TearDown]
        public void TearDown()
        {
            dest.Close();
            admin.Close();
        }

        [Test]
        public async Task send_completes_when_delivered_to_queue()
        {
            using (var sender = new Sender(adminFormatName))
            {
                await sender.StartAsync();
                var msg = new Message { Label = "send1" };
                await sender.SendAsync(msg, QueueTransaction.Single, dest);
            }
        }

        [Test]
        public async Task send_throw_exception_when_sending_non_transactional_message_to_transactional_queue()
        {
            using (var sender = new Sender(adminFormatName))
            {
                await sender.StartAsync();
                var msg = new Message { Label = "send2" };
                try
                {
                    await sender.SendAsync(msg, QueueTransaction.None, dest);
                    Assert.Fail("Exception not thrown");
                }
                catch (AcknowledgmentException ex)
                {
                    Assert.AreEqual(MessageClass.NotTransactionalMessage, ex.Acknowledgment2);
                }
            }
        }

        [Test]
        public async Task send_throw_exception_when_destination_machine_does_not_exist()
        {
            using (var doesNotExist = Queue.Open("FormatName:Direct=OS:not.known.server\\private$\\some-queue", QueueAccessMode.Send))
            using (var sender = new Sender(adminFormatName))
            {
                sender.ReachQueueTimeout = TimeSpan.FromMilliseconds(100);
                await sender.StartAsync();
                var msg = new Message { Label = "send3" };
                try
                {
                    await sender.SendAsync(msg, QueueTransaction.Single, doesNotExist);
                    Assert.Fail("Exception not thrown");
                }
                catch (AcknowledgmentException ex)
                {
                    Assert.AreEqual(MessageClass.ReachQueueTimeout, ex.Acknowledgment2);
                }
            }
        }

    }
}
