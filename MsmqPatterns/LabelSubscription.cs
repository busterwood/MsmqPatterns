using BusterWood.Msmq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace BusterWood.MsmqPatterns
{
    /// <summary>
    /// A class to manage subscriptions to messages based on the message label.
    /// You can subscribe with <see cref="WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
    /// You can subscribe with <see cref="AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
    /// </summary>
    public class LabelSubscription 
    {
        readonly Node _root = new Node("");

        /// <summary>The path separator to use, defaults to dot (.)</summary>
        public char Separator { get; set; } = '.';

        /// <summary>The wild-card string to use, defaults to star (*) </summary>
        public string WildCard { get; set; } = "*";

        /// <summary>The all descendants wild-card string to use, defaults to star-star (**) </summary>
        public string AllDecendents { get; set; } = "**";

        /// <summary>
        /// Subscribe to messages with a matching <paramref name="label"/>, invoking the <paramref name="callback"/> when <see cref="Dispatch(Message)"/> is called.
        /// You can subscribe with <see cref="WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
        /// You can subscribe with <see cref="AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
        /// </summary>
        /// <returns>A handle to that unsubscribes when it is Disposed</returns>
        public IDisposable Subscribe(string label, Action<Message> callback)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));
            Contract.Requires(callback != null);

            var star = label.IndexOf('*');
            if (star >= 0)
            {
                var remain = label.Substring(star);
                if (remain != WildCard && remain != AllDecendents)
                    throw new ArgumentException("wildcard subscriptions are only supported as the last character");
            }

            var parts = label.Split(Separator);
            lock (_root)
            {
                var node = _root;
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part.Length == 0)
                        throw new ArgumentException("label contains an empty part");

                    Contract.Assume(node != null);
                    if (node.ChildNodes == null)
                        node.ChildNodes = new Dictionary<string, Node>();

                    Node child;
                    if (!node.ChildNodes.TryGetValue(part, out child))
                        node.ChildNodes.Add(part, child = new Node(part));
                    node = child;
                }

                node.Subscriptions += callback;
                return new Unsubscribe(node, callback, _root);
            }
        }

        /// <summary>Sends the message to the all the subscribers.</summary>
        public void Dispatch(Message message)
        {
            Contract.Requires(message != null);
            Contract.Requires(!string.IsNullOrEmpty(message.Label));

            Action<Message> subscriptions = Subscribers(message.Label);
            if (subscriptions == null)
                return;

            foreach (Action<Message> callback in subscriptions.GetInvocationList())
            {
                try
                {
                    callback(message);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("WARN: " + ex);
                }
            }
        }

        /// <summary>Returns the subscriber callbacks for a message label</summary>
        public Action<Message> Subscribers(string messageLabel)
        {
            Contract.Requires(!string.IsNullOrEmpty(messageLabel));

            var parts = messageLabel.Split(Separator);
            lock (_root)
            {
                Action<Message> subscriptions = null;
                var node = _root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    Node child;
                    if (node.ChildNodes == null)
                        return subscriptions;
                    if (node.ChildNodes.TryGetValue(AllDecendents, out child))
                        subscriptions += child.Subscriptions;
                    if (!node.ChildNodes.TryGetValue(parts[i], out child))
                        return subscriptions;
                    node = child;
                }

                Contract.Assume(node != null);

                var lastPart = parts[parts.Length - 1];

                Node lastChild;

                if (node.ChildNodes.TryGetValue(lastPart, out lastChild))
                    subscriptions += lastChild.Subscriptions;

                if (node.ChildNodes.TryGetValue(WildCard, out lastChild))
                    subscriptions += lastChild.Subscriptions;

                if (node.ChildNodes.TryGetValue(AllDecendents, out lastChild))
                    subscriptions += lastChild.Subscriptions;

                return subscriptions;
            }
        }

        class Node 
        {
            public readonly string Name;
            public Dictionary<string, Node> ChildNodes;
            public Action<Message> Subscriptions;

            public Node(string name)
            {
                Name = name;
            }
        }


        class Unsubscribe : IDisposable
        {
            readonly Action<Message> callback;
            readonly Node node;
            readonly Node root;

            public Unsubscribe(Node node, Action<Message> callback, Node root)
            {
                this.node = node;
                this.callback = callback;
                this.root = root;
            }

            public void Dispose()
            {
                lock(root)
                    node.Subscriptions -= callback;
            }
        }
    }
}
