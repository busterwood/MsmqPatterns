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
            if (Queue.Exists(path))
                Queue.TryDelete(Queue.PathToFormatName(path));

            var fn = Queue.TryCreate(path, QueueTransactional.Transactional);

            var postQ = Queue.Open(fn, QueueAccessMode.Send);
            var postMsg = new Message { AppSpecific = 1, Label = "async1", Journal = Journal.DeadLetter, Delivery = Delivery.Express };
            postMsg.BodyUTF8(string.Join(Environment.NewLine, Enumerable.Repeat("hello world! and hello again", 9000)));
            postQ.Post(postMsg, Transaction.Single);


            var readQ = Queue.Open(fn, QueueAccessMode.ReceiveAndPeek);
            try
            {
                var peeked = readQ.Peek(Properties.AppSpecific | Properties.Label | Properties.LookupId);

                var moveQ = Queue.Open(fn + ";test", QueueAccessMode.Move);
                readQ.Move(peeked.LookupId, moveQ, Transaction.Single);

                var subQ = Queue.Open(fn + ";test", QueueAccessMode.ReceiveAndPeek);
                var msg = subQ.Receive(Properties.All, peeked.LookupId, transaction: Transaction.Single);

                var body = msg.BodyUTF8();
                var l = msg.Label;
                var ttr = msg.TimeToBeReceived;
                var ttrq = msg.TimeToReachQueue;
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
        }
    }
}
