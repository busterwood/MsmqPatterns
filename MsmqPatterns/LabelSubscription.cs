using BusterWood.Msmq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace BusterWood.MsmqPatterns
{
    public class LabelSubscription 
    {
        const string WildCard = "*";
        static readonly char[] labelSeparator = { '.' };
        readonly Node _root = new Node("");

        public IDisposable Subscribe(string label, Action<Message> callback)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));
            Contract.Requires(callback != null);

            var star = label.IndexOf('*');
            if (star >= 0 && star != label.Length-1)
                throw new ArgumentException("wildcard subscriptions are only supported as the last character");

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

        public Action<Message> Subscribers(string label)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));

            var parts = label.Split(labelSeparator);
            lock (_root)
            {

                var node = _root;
                foreach (var part in parts)
                {
                    if (part.Length == 0)
                        throw new ArgumentException("label contains an empty part");

                    Contract.Assume(node != null);
                    Node child;
                    if (node.ChildNodes == null || !node.ChildNodes.TryGetValue(part, out child))
                        return null;
                    Contract.Assume(child != null);
                    node = child;
                }
                return node.Subscriptions;
            }
        }

        public void Dispatch(Message msg)
        {
            Contract.Requires(msg != null);

            Action<Message> subscriptions = null;
            var parts = msg.Label.Split(labelSeparator);
            lock (_root)
            {
                var node = _root;
                for (int i = 0; i < parts.Length-1; i++)
                {
                    Node child;
                    if (node.ChildNodes == null || !node.ChildNodes.TryGetValue(parts[i], out child))
                        return; // no subscriptions
                    node = child;
                }

                var lastPart = parts[parts.Length-1];
                Node lastChild;
                if (node.ChildNodes.TryGetValue(lastPart, out lastChild))
                {
                    subscriptions += lastChild.Subscriptions;
                }
                if (node.ChildNodes.TryGetValue(WildCard, out lastChild)) // wild card subscription
                {
                    subscriptions += lastChild.Subscriptions;
                }
            }

            if (subscriptions != null)
            {
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
