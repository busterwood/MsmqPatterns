using System;
using BusterWood.Msmq;
using System.Linq;
using BusterWood.Msmq.Patterns;
using System.Diagnostics;

namespace ConsoleApplication1
{
    /// <summary>
    /// Sample MsmqCache client
    /// </summary>
    class Program
    {

        static void Main(string[] args)
        {
            var requestQueue = new QueueWriter("multicast=224.3.9.8:234");

            var process = Process.GetCurrentProcess();
            var replyQueueFormatName = Queue.TryCreate(Queue.NextTempQueuePath(), QueueTransactional.None, label: process.ProcessName + ":" + process.Id);
            var replyQueue = new QueueReader(replyQueueFormatName, share: QueueShareReceive.ExclusiveReceive);
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
                            var msg = new Message { Label = "cache." + bits[1], ResponseQueue = replyQueueFormatName };
                            sw.Restart();
                            requestQueue.Write(msg); // multicast
                            var reply = replyQueue.Read(timeout: TimeSpan.FromSeconds(1));
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
                            requestQueue.Write(msg); // multicast
                            break;
                        }
                    case "remove":
                        {
                            var msg = new Message { Label = "cache." + bits[1], AppSpecific=(int)MessageCacheAction.Remove };
                            if (bits.Length > 2)
                                msg.BodyUTF8(bits[2]);
                            requestQueue.Write(msg); // multicast
                            break;
                        }
                    case "clear":
                        {
                            var msg = new Message { Label = "cache", AppSpecific=(int)MessageCacheAction.Clear };
                            if (bits.Length > 2)
                                msg.BodyUTF8(bits[2]);
                            requestQueue.Write(msg); // multicast
                            break;
                        }
                    case "list":
                        {
                            var msg = new Message { Label = "cache", AppSpecific=(int)MessageCacheAction.ListKeys, ResponseQueue = replyQueueFormatName };
                            sw.Restart();
                            requestQueue.Write(msg); // multicast
                            var reply = replyQueue.Read(timeout: TimeSpan.FromSeconds(1));
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
                }
            }

        }
    }
}
