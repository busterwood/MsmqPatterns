using BusterWood.Msmq;
using BusterWood.Msmq.Patterns;
using NUnit.Framework;


namespace UnitTests.Patterns
{
    [TestFixture]
    public class PostmanTests
    {
        public void fred()
        {
            QueueWriter q = null;
            Message msg = null;
            Postman pm = null;
            q.Deliver(msg, pm);
        }
    }
}
