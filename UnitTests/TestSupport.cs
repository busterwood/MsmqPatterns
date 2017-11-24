using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests
{
    static class TestSupport
    {
        public static void ReadAllMessages(string path)
        {
            using (var q = new MessageQueue(path, QueueAccessMode.Receive))
            {
                for (;;)
                {
                    try
                    {
                        q.Receive(TimeSpan.FromMilliseconds(10)).Dispose();
                    }
                    catch (MessageQueueException ex) when (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                    {
                        break;
                    }
                }
            }
        }

    }
}
