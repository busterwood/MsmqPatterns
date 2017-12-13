using System;
using BusterWood.Msmq;
using System.Linq;

namespace ConsoleApplication1
{
    class Program
    {
        private const string path = ".\\private$\\ping";

        static void Main(string[] args)
        {
            //if (Queue.Exists(path))
            //    Queue.TryDelete(Queue.PathToFormatName(path));

            var fn = Queue.TryCreate(path, QueueTransactional.Transactional);

            var postQ = new QueueWriter(fn);
            var postMsg = new Message { AppSpecific = 1, Label = "async1", Journal = Journal.DeadLetter, Delivery = Delivery.Express };
            postMsg.BodyUTF8(string.Join(Environment.NewLine, Enumerable.Repeat("hello world! and hello again", 9000)));
            postMsg.ExtensionUTF8("context-type: text/utf-8");
            postQ.Post(postMsg, QueueTransaction.Single);

            var readQ = new QueueReader(fn);
            try
            {
                var peeked = readQ.Peek(Properties.AppSpecific | Properties.Label | Properties.LookupId, transaction: QueueTransaction.Single);
                GC.KeepAlive(peeked.CorrelationId);
                var moveQ = new SubQueueMover(fn + ";test");
                moveQ.MoveFrom(readQ, peeked.LookupId, QueueTransaction.Single);

                var subQ = new QueueReader(fn + ";test");
                var msg = subQ.Receive(Properties.All, peeked.LookupId, transaction: QueueTransaction.Single);

                var body = msg.BodyUTF8();
                var l = msg.Label;
                var ttr = msg.TimeToBeReceived;
                var sq = subQ.SubQueueName();
                var ttrq = msg.TimeToReachQueue;
                var ext = msg.ExtensionUTF8();
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }
    }
}
