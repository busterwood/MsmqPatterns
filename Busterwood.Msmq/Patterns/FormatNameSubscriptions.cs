using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// A class to manage subscriptions to messages based on the message label.  All methods are thread-safe.
    /// You can subscribe with <see cref="WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
    /// You can subscribe with <see cref="AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
    /// </summary>
    class FormatNameSubscriptions
    {
        readonly Dictionary<Subscriber, HashSet<string>> _subscriptionsByKey = new Dictionary<Subscriber, HashSet<string>>();
        readonly Node _root = new Node("");

        /// <summary>The path separator to use, defaults to dot (.)</summary>
        public char Separator { get; set; } = '.';

        /// <summary>The wild-card string to use, defaults to star (*) </summary>
        public string WildCard { get; set; } = "*";

        /// <summary>The all descendants wild-card string to use, defaults to star-star (**) </summary>
        public string AllDecendents { get; set; } = "**";

        public object SyncRoot => _root;

        /// <summary>
        /// Subscribe to messages with a matching <paramref name="label"/>, invoking the <paramref name="formatName"/> when <see cref="Dispatch(Message)"/> is called.
        /// You can subscribe with <see cref="WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
        /// You can subscribe with <see cref="AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
        /// </summary>
        /// <returns>A handle to that unsubscribes when it is Disposed</returns>
        public void Add(string label, string formatName, string tag = null)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));
            Contract.Requires(formatName != null);

            var star = label.IndexOf('*');
            if (star >= 0)
            {
                var remain = label.Substring(star);
                if (remain != WildCard && remain != AllDecendents)
                    throw new ArgumentException("wildcard subscriptions are only supported as the last character");
            }

            var key = new Subscriber(formatName, tag);
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

                node.Subscriptions.Add(key);

                // add to format name specific set of subscriptions
                HashSet<string> subs;
                if (!_subscriptionsByKey.TryGetValue(key, out subs))
                    subs = new HashSet<string>();
                subs.Add(label);
            }
        }
       
        /// <summary>Returns the response queue format names that are subscribed to a label</summary>
        public HashSet<Subscriber> GetSubscribers(string label, string tag = null)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));

            var parts = label.Split(Separator);

            lock (_root)
            {
                HashSet<Subscriber> subscriptions = new HashSet<Subscriber>();
                var node = _root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    Node child;
                    if (node.ChildNodes == null)
                        return subscriptions;
                    if (node.ChildNodes.TryGetValue(AllDecendents, out child))
                        subscriptions.UnionWith(child.Subscriptions);
                    if (!node.ChildNodes.TryGetValue(parts[i], out child))
                        return subscriptions;
                    node = child;
                }

                Contract.Assume(node != null);

                var lastPart = parts[parts.Length - 1];

                Node lastChild;

                if (node.ChildNodes.TryGetValue(lastPart, out lastChild))
                    subscriptions.UnionWith(lastChild.Subscriptions);

                if (node.ChildNodes.TryGetValue(WildCard, out lastChild))
                    subscriptions.UnionWith(lastChild.Subscriptions);

                if (node.ChildNodes.TryGetValue(AllDecendents, out lastChild))
                    subscriptions.UnionWith(lastChild.Subscriptions);

                return subscriptions;
            }
        }

        /// <summary>Remove subscriptions for a label and formatName</summary>
        public void Remove(string label, string formatName, string tag = null)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));

            var parts = label.Split(Separator);
            var key = new Subscriber(formatName, tag);
            lock (_root)
            {
                HashSet<string> subs;
                if (!_subscriptionsByKey.TryGetValue(key, out subs) || !subs.Remove(label))
                    return;

                RemoveCore(parts, key);
            }
        }

        public void RemoveCore(string[] labelParts, Subscriber key)
        {
            var node = _root;
            for (int i = 0; i < labelParts.Length; i++)
            {
                Node child;
                if (node.ChildNodes == null)
                    return;
                if (!node.ChildNodes.TryGetValue(labelParts[i], out child))
                    return;
                node = child;
            }

            Contract.Assume(node != null);
            node.Subscriptions.Remove(key);
        }

        /// <summary>Removes all subscriptions for a format name</summary>
        public void Clear(string formatName, string tag = null)
        {
            var key = new Subscriber(formatName, tag);

            lock (_root)
            {
                HashSet<string> labels;
                if (!_subscriptionsByKey.TryGetValue(key, out labels))
                    return;

                foreach (var label in labels)
                {
                    var parts = label.Split(Separator);
                    RemoveCore(parts, key);
                }
            }
        }

        /// <summary>Gets the labels that a format name is currently subscribed too</summary>
        public HashSet<string> GetSubscriptions(string formatName, string tag = null)
        {
            var key = new Subscriber(formatName, tag);
            lock (_root)
            {
                HashSet<string> subs;
                if (!_subscriptionsByKey.TryGetValue(key, out subs))
                    return new HashSet<string>();
                return new HashSet<string>(subs);
            }
        }

        public struct Subscriber
        {
            public string FormatName { get; }
            public string Tag { get; }

            public Subscriber(string formatName, string tag) : this()
            {
                FormatName = formatName;
                Tag = tag;
            }
        }

        class Node 
        {
            /// <summary>The name of this node</summary>
            public readonly string Name;

            /// <summary>The child nodes, if any (can be null)</summary>
            public Dictionary<string, Node> ChildNodes;

            /// <summary>The format names of the response queues that are subscribed to this node</summary>
            public HashSet<Subscriber> Subscriptions = new HashSet<Subscriber>();

            public Node(string name)
            {
                Name = name;
            }
        }

    }
}
