using BusterWood.Msmq;
using BusterWood.MsmqPatterns;
using NUnit.Framework;
using System;

namespace UnitTests
{
    [TestFixture]
    public class LabelSubscriptionTests
    {
        [TestCase("fred")]
        [TestCase("hello.world")]
        public void can_subscribe(string label)
        {
            var sub = new LabelSubscription();
            Action<Message> callback = msg => { };
            sub.Subscribe(label, callback);
            var actual = sub.Subscribers(label);
            Assert.AreEqual(callback, actual);
        }

        [Test]
        public void can_subscribe_multiple_times()
        {
            var sub = new LabelSubscription();
            Action<Message> callback1 = msg => { };
            Action<Message> callback2 = msg => { };
            sub.Subscribe("fred", callback1);
            sub.Subscribe("fred", callback2);
            var actual = sub.Subscribers("fred");
            var subscriptions = actual.GetInvocationList();
            Assert.IsTrue(Array.IndexOf(subscriptions, callback1)  >= 0);
            Assert.IsTrue(Array.IndexOf(subscriptions, callback2)  >= 0);
        }

        [Test]
        public void can_unsubscribe()
        {
            var sub = new LabelSubscription();
            Action<Message> callback = msg => { };
            var unsub = sub.Subscribe("fred", callback);
            unsub.Dispose();
            var actual = sub.Subscribers("fred");
            Assert.IsNull(actual);
        }

        [TestCase("hello.world", "hello.world")]
        [TestCase("hello.*", "hello.world")]
        [TestCase("*", "hello")]
        public void can_dispatch(string subscriptionLabel, string msgLabel)
        {
            var sub = new LabelSubscription();
            Message actual = null;
            Action<Message> callback = msg => actual = msg;
            sub.Subscribe(subscriptionLabel, callback);
            var expected = new Message { Label = msgLabel };
            sub.Dispatch(expected);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void an_error_in_the_callback_does_not_propergate_to_the_caller()
        {
            var sub = new LabelSubscription();
            Action<Message> callback = msg => { throw new InvalidOperationException(); };
            sub.Subscribe("hello", callback);
            var expected = new Message { Label = "hello" };
            sub.Dispatch(expected);
            Assert.Pass();
        }

        [Test]
        public void an_error_in_a_callback_does_not_prevent_other_callbacks_from_being_called()
        {
            var sub = new LabelSubscription();
            bool got = false;
            Action<Message> callback1 = msg => { throw new InvalidOperationException(); };
            Action<Message> callback2 = msg => { got = true; };
            sub.Subscribe("hello", callback1);
            sub.Subscribe("hello", callback2);
            var expected = new Message { Label = "hello" };
            sub.Dispatch(expected);
            Assert.AreEqual(true, got);
        }
    }
}
