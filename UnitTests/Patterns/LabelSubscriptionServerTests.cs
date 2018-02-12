using BusterWood.Msmq;
using BusterWood.Msmq.Patterns;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.Patterns
{
    [TestFixture]
    public class LabelSubscriptionServerTests
    {
        string dataFN;
        string requestFN;
        string replyFN;
        LabelSubscriptionServer server;

        [SetUp]
        public void Setup()
        {
            dataFN = Queues.TryCreate(@".\private$\server.data", QueueTransactional.None);
            requestFN = Queues.TryCreate(@".\private$\server.control", QueueTransactional.None);
            replyFN = Queues.TryCreate(@".\private$\server.client1", QueueTransactional.None);

            Queues.Purge(dataFN);
            Queues.Purge(requestFN);
            Queues.Purge(replyFN);

            server = new LabelSubscriptionServer(requestFN, dataFN);
            server.StartAsync().Wait();
        }

        [TearDown]
        public void TearDown()
        {
            server.Dispose();
        }

        [Test]
        public async Task can_subscribe()
        {
            using (var requestWriter = new QueueWriter(requestFN))
            using (var replyReader = new QueueReader(replyFN))
            {
                var addReq = new Message { AppSpecific = (int)PubSubAction.Add, ResponseQueue = replyFN };
                addReq.BodyUTF8("thing.one");
                requestWriter.Write(addReq);

                var listReq = new Message { AppSpecific = (int)PubSubAction.List, ResponseQueue = replyFN };
                requestWriter.Write(listReq);

                var got = replyReader.Read(timeout: TimeSpan.FromSeconds(3));
                Assert.IsNotNull(got);
                Assert.AreEqual("thing.one", got.BodyUTF8());
            }
        }

        [Test]
        public async Task can_add_mutliple_subscriptions()
        {
            using (var requestWriter = new QueueWriter(requestFN))
            using (var replyReader = new QueueReader(replyFN))
            {
                var addReq = new Message { AppSpecific = (int)PubSubAction.Add, ResponseQueue = replyFN };
                addReq.BodyUTF8("thing.one" + Environment.NewLine + "thing.two");
                requestWriter.Write(addReq);

                var listReq = new Message { AppSpecific = (int)PubSubAction.List, ResponseQueue = replyFN };
                requestWriter.Write(listReq);

                var got = replyReader.Read(timeout: TimeSpan.FromSeconds(3));
                Assert.IsNotNull(got);
                var lines = new StringReader(got.BodyUTF8());
                Assert.AreEqual("thing.one", lines.ReadLine());
                Assert.AreEqual("thing.two", lines.ReadLine());
            }
        }

        [Test]
        public async Task can_remove_subscription()
        {
            using (var requestWriter = new QueueWriter(requestFN))
            using (var replyReader = new QueueReader(replyFN))
            {
                var addReq = new Message { AppSpecific = (int)PubSubAction.Add, ResponseQueue = replyFN };
                addReq.BodyUTF8("thing.one" + Environment.NewLine + "thing.two");
                requestWriter.Write(addReq);

                var removeReq = new Message { AppSpecific = (int)PubSubAction.Remove, ResponseQueue = replyFN };
                removeReq.BodyUTF8("thing.one");
                requestWriter.Write(removeReq);

                var listReq = new Message { AppSpecific = (int)PubSubAction.List, ResponseQueue = replyFN };
                requestWriter.Write(listReq);

                var got = replyReader.Read(timeout: TimeSpan.FromSeconds(3));
                Assert.IsNotNull(got);
                Assert.AreEqual("thing.two", got.BodyUTF8());
            }
        }
    }
}
