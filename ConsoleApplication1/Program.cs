using System;
using BusterWood.Msmq;
using System.Linq;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var fn = Queue.PathToFormatName(".\\private$\\ping");
            var postQ = Queue.Open(fn, QueueAccessMode.Send);
            var postMsg = new Message { AppSpecific = 1, Label = "async1" };
            postMsg.BodyUTF8(string.Join(Environment.NewLine, Enumerable.Repeat("hello world! and hello again", 1000)));
            postQ.Post(postMsg);

            var readQ = Queue.Open(fn, QueueAccessMode.Receive);
            try
            {
                var task = readQ.ReceiveAsync(Properties.All);
                var msg = task.Result;
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
