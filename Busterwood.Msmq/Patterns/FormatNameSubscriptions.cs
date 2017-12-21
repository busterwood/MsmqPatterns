using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace BusterWood.Msmq.Patterns
{
    /// <summary>
    /// A class to manage subscriptions to messages based on the message label.
    /// You can subscribe with <see cref="WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
    /// You can subscribe with <see cref="AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
    /// </summary>
    public class FormatNameSubscriptions
    {
        readonly Node _root = new Node("");

        /// <summary>The path separator to use, defaults to dot (.)</summary>
        public char Separator { get; set; } = '.';

        /// <summary>The wild-card string to use, defaults to star (*) </summary>
        public string WildCard { get; set; } = "*";

        /// <summary>The all descendants wild-card string to use, defaults to star-star (**) </summary>
        public string AllDecendents { get; set; } = "**";

        /// <summary>
        /// Subscribe to messages with a matching <paramref name="label"/>, invoking the <paramref name="formatName"/> when <see cref="Dispatch(Message)"/> is called.
        /// You can subscribe with <see cref="WildCard"/>, e.g. "hello.*" will match a message with label "hello.world".
        /// You can subscribe with <see cref="AllDecendents"/> , e.g. "hello.**" will match a message with label "hello.world.1.2.3".
        /// </summary>
        /// <returns>A handle to that unsubscribes when it is Disposed</returns>
        public void Subscribe(string label, string formatName)
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

                node.Subscriptions.Add(formatName);
            }
        }
       
        /// <summary>Returns the subscriber callbacks for a message label</summary>
        public HashSet<string> Subscribers(string label)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));

            var parts = label.Split(Separator);
            lock (_root)
            {
                HashSet<string> subscriptions = new HashSet<string>();
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
        public void Unsubscribe(string label, string formatName)
        {
            Contract.Requires(!string.IsNullOrEmpty(label));

            var parts = label.Split(Separator);
            lock (_root)
            {
                var node = _root;
                for (int i = 0; i < parts.Length; i++)
                {
                    Node child;
                    if (node.ChildNodes == null)
                        return;
                    if (!node.ChildNodes.TryGetValue(parts[i], out child))
                        return;
                    node = child;
                }

                Contract.Assume(node != null);
                node.Subscriptions.Remove(formatName);
            }
        }

        class Node 
        {
            public readonly string Name;
            public Dictionary<string, Node> ChildNodes;
            public HashSet<string> Subscriptions = new HashSet<string>();

            public Node(string name)
            {
                Name = name;
            }
        }

      
    }
}
