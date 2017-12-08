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
            bool exists = Queue.Exists(path);

            var fn = Queue.TryCreate(path, QueueTransactional.None);

            var postQ = Queue.Open(fn, QueueAccessMode.Send);
            var postMsg = new Message { AppSpecific = 1, Label = "async1" };
            postMsg.BodyUTF8(string.Join(Environment.NewLine, Enumerable.Repeat("hello world! and hello again", 9000)));
            postQ.Post(postMsg);

            var moveQ = Queue.Open(fn + ";test", QueueAccessMode.Move);

            var readQ = Queue.Open(fn, QueueAccessMode.ReceiveAndPeek);
            try
            {
                var task = readQ.PeekAsync(Properties.AppSpecific | Properties.Label | Properties.LookupId);
                var peeked = task.Result;

                readQ.Move(peeked.LookupId, moveQ);

                var subQ = Queue.Open(fn + ";test", QueueAccessMode.ReceiveAndPeek);
                var msg = subQ.ReceiveByLookupId(Properties.All, peeked.LookupId);

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
