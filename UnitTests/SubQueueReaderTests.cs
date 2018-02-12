using BusterWood.Msmq;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class SubQueueReaderTests
    {
        [Test]
        public void can_peek_when_opened_with_move_acces()
        {
            var fn = Queues.TryCreate(".\\private$\\subqtest", QueueTransactional.None);
            var sqfn = fn + ";sq";

            using (var qWriter = new QueueWriter(fn))
            {
                qWriter.Write(new Message { AppSpecific = 234 });
            }
            using (var qReader = new QueueReader(fn))
            using (var subQueue = new SubQueue(sqfn))
            {
                var msg = qReader.Peek(Properties.LookupId);
                Queues.MoveMessage(qReader, subQueue, msg.LookupId);
                var got = subQueue.Read();
                Assert.AreEqual(234, got.AppSpecific);
            }
        }
    }
}
