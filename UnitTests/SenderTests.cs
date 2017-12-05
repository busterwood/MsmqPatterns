using MsmqPatterns;
using NUnit.Framework;
using System;
using System.Messaging;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture, Timeout(1000)]
    public class SenderTests
    {
        static string destQueueName = $".\\private$\\{nameof(SenderTests)}.Input";
        string adminQueueName = $".\\private$\\{nameof(SenderTests)}.Admin";
        MessageQueue dest;
        MessageQueue admin;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            if (!MessageQueue.Exists(destQueueName))
                MessageQueue.Create(destQueueName, true);
            if (!MessageQueue.Exists(adminQueueName))
                MessageQueue.Create(adminQueueName, false); // cannot be transactional
        }

        [SetUp]
        public void Setup()
        {
            if (MessageQueue.Exists(destQueueName))
                TestSupport.ReadAllMessages(destQueueName);
            if (MessageQueue.Exists(adminQueueName))
                TestSupport.ReadAllMessages(adminQueueName);

            dest = new MessageQueue(destQueueName, QueueAccessMode.SendAndReceive);
            admin = new MessageQueue(adminQueueName, QueueAccessMode.Receive);
        }

        [Test]
        public async Task send_completes_when_delivered_to_queue()
        {
            using (var sender = new Sender(admin))
            {
                await sender.StartAsync();
                var msg = new Message { Label = "send1" };
                await sender.SendAsync(msg, MessageQueueTransactionType.Single, dest);
            }
        }

        [Test]
        public async Task send_throw_exception_when_sending_non_transactional_message_to_transactional_queue()
        {
            using (var sender = new Sender(admin))
            {
                await sender.StartAsync();
                var msg = new Message { Label = "send2" };
                try
                {
                    await sender.SendAsync(msg, MessageQueueTransactionType.None, dest);
                    Assert.Fail("Exception not thrown");
                }
                catch (AcknowledgmentException ex)
                {
                    Assert.AreEqual(Acknowledgment.NotTransactionalMessage, ex.Acknowledgment);
                }
            }
        }

        [Test]
        public async Task send_throw_exception_when_destination_machine_does_not_exist()
        {
            using (var doesNotExist = new MessageQueue("FormatName:Direct=OS:notknownserver\\private$\\some-queue"))
            using (var sender = new Sender(admin))
            {
                sender.ReachQueueTimeout = TimeSpan.FromMilliseconds(100);
                await sender.StartAsync();
                var msg = new Message { Label = "send3" };
                try
                {
                    await sender.SendAsync(msg, MessageQueueTransactionType.Single, doesNotExist);
                    Assert.Fail("Exception not thrown");
                }
                catch (AcknowledgmentException ex)
                {
                    Assert.AreEqual(Acknowledgment.ReachQueueTimeout, ex.Acknowledgment);
                }
            }
        }

    }
}
