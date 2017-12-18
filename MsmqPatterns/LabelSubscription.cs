using BusterWood.Msmq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace BusterWood.MsmqPatterns
{
    public class LabelSubscription 
    {
        const string WildCard = "*";
        const string AllDecendents = "**";
        static readonly char[] labelSeparator = { '.' };
        readonly Node _root = new Node("");

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

            var parts = label.Split(labelSeparator);
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

        public void Dispatch(Message msg)
        {
            Contract.Requires(msg != null);
            Contract.Requires(!string.IsNullOrEmpty(msg.Label));

            Action<Message> subscriptions = Subscribers(msg.Label);
            if (subscriptions == null)
                return;

            foreach (Action<Message> callback in subscriptions.GetInvocationList())
            {
                try
                {
                    callback(msg);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("WARN: " + ex);
                }
            }
        }

        public Action<Message> Subscribers(string messageLabel)
        {
            Contract.Requires(!string.IsNullOrEmpty(messageLabel));

            var parts = messageLabel.Split(labelSeparator);
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
