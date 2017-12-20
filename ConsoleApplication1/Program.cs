using System;
using BusterWood.Msmq;
using System.Linq;
using BusterWood.Msmq.Patterns;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    /// <summary>
    /// Sample MsmqCache client
    /// </summary>
    class Program
    {

        static void Main(string[] args)
        {
            //var requestQueueFormatName = "multicast=224.3.9.8:234";
            var requestQueueFormatName = Queue.PathToFormatName(".\\private$\\cache.input");
            var requestQueue = new QueueWriter(requestQueueFormatName);

            var process = Process.GetCurrentProcess();
            var replyQueueFormatName = Queue.TryCreate(Queue.NextTempQueuePath(), QueueTransactional.None, label: process.ProcessName + ":" + process.Id);

            var adminQueueFormatName = Queue.TryCreate(Queue.NextTempQueuePath(), QueueTransactional.None, label: "Admin " + process.ProcessName + ":" + process.Id);

            var postman = new Postman(adminQueueFormatName) { ReachQueueTimeout = TimeSpan.FromSeconds(30) };
            postman.StartAsync();

            var rr = new RequestReply(requestQueueFormatName, replyQueueFormatName, postman);

            var sw = new Stopwatch();
            for (;;)
            {
                var line = Console.ReadLine();
                if (line.Length == 0)
                    break;

                var bits = line.Split(' ');
                switch (bits[0].ToLower())
                {
                    case "get":
                        {
                            var msg = new Message { Label = "cache." + bits[1], ResponseQueue = replyQueueFormatName, SenderIdType = SenderIdType.None };
                            sw.Restart();
                            var reply = rr.SendRequest(msg);
                            sw.Stop();
                            if (reply == null)
                                Console.Error.WriteLine("*** no reply");
                            else
                                Console.WriteLine($"got {reply.Label} {reply.BodyUTF8()} in {sw.Elapsed.TotalMilliseconds:N1}MS");
                            break;
                        }
                    case "put":
                        {
                            var msg = new Message { Label = bits[1] };
                            if (bits.Length > 2)
                                msg.BodyUTF8(bits[2]);
                            postman.Deliver(msg, requestQueue); // multicast
                            break;
                        }
                    case "remove":
                        {
                            var msg = new Message { Label = "cache." + bits[1], AppSpecific=(int)MessageCacheAction.Remove };
                            if (bits.Length > 2)
                                msg.BodyUTF8(bits[2]);
                            postman.Deliver(msg, requestQueue); // multicast
                            break;
                        }
                    case "clear":
                        {
                            var msg = new Message { Label = "cache", AppSpecific=(int)MessageCacheAction.Clear };
                            if (bits.Length > 2)
                                msg.BodyUTF8(bits[2]);
                            postman.Deliver(msg, requestQueue); // multicast
                            break;
                        }
                    case "list":
                        {
                            var msg = new Message { Label = "cache", AppSpecific=(int)MessageCacheAction.ListKeys, ResponseQueue = replyQueueFormatName };
                            sw.Restart();
                            var reply = rr.SendRequest(msg);
                            sw.Stop();
                            if (reply == null)
                                Console.Error.WriteLine("*** no reply");
                            else
                            {
                                Console.WriteLine(reply.BodyUTF8());
                                Console.WriteLine($"listing keys took {sw.Elapsed.TotalMilliseconds:N1}MS");
                            }
                            break;
                        }

                    case "puts":
                        {
                            sw.Restart();
                            var tracking = new List<Tracking>(1000);
                            for (int i = 1; i <= 1000; i++)
                            {
                                var msg = new Message { Label = "price."+i, SenderIdType = SenderIdType.Sid };
                                msg.BodyUTF8($"bid={i-0.1m:N1},ask={i + 0.1m:N1}");
                                tracking.Add(postman.RequestDelivery(msg, requestQueue)); // multicast
                            }
                            try
                            {
                                Task.WaitAll(tracking.Select(postman.WaitForDelivery).ToArray());
                                Console.WriteLine($"Sent 1000 messages in {sw.Elapsed.TotalSeconds:N1} seconds");
                            }
                            catch (AggregateException ex)
                            {
                                Console.Error.WriteLine(ex.InnerException.Message);
                            }
                            break;
                        }
                }
            }

        }
    }
}
