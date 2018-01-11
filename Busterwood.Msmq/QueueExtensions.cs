using System;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    public static class QueueExtensions
    {
        static readonly Encoding utf8NoBom = new UTF8Encoding(false);

        public static string SubQueueName(this Queue queue)
        {
            Contract.Requires(queue != null);
            var fn = queue.FormatName;
            int idx = fn.IndexOf(';');
            return idx < 0 ? "" : fn.Substring(idx + 1);
        }

        public static void BodyUTF8(this Message msg, string text)
        {
            Contract.Requires(msg != null);
            Contract.Requires(text != null);

            msg.Body = utf8NoBom.GetBytes(text);
            msg.BodyType = BodyType.ByteArray;
        }

        public static string BodyUTF8(this Message msg)
        {
            Contract.Requires(msg != null);
            Contract.Requires(msg.BodyType == BodyType.ByteArray || msg.BodyType == BodyType.None);

            var buf = msg.Body;
            return buf == null ? null : utf8NoBom.GetString(buf);
        }

        public static void BodyASCII(this Message msg, string text)
        {
            Contract.Requires(msg != null);
            Contract.Requires(text != null);

            var count = Encoding.ASCII.GetByteCount(text);
            var buf = new byte[count + 1]; // one extra for null char
            Encoding.ASCII.GetBytes(text, 0, text.Length, buf, 0);
            msg.Body = buf;
            msg.BodyType = BodyType.AnsiString;
        }

        public static string BodyASCII(this Message msg)
        {
            Contract.Requires(msg != null);
            Contract.Requires(msg.BodyType == BodyType.AnsiString);

            var buf = msg.Body;
            if (buf == null) return null;
            var chars = buf.Length > 0 && buf[buf.Length - 1] == 0 ? buf.Length - 1 : buf.Length; // remove trailing null
            return Encoding.ASCII.GetString(buf, 0, chars);
        }

        public static void BodyUTF16(this Message msg, string text)
        {
            Contract.Requires(msg != null);
            Contract.Requires(text != null);

            var count = Encoding.Unicode.GetByteCount(text);
            var buf = new byte[count + 2]; // 2 extra for null char
            Encoding.Unicode.GetBytes(text, 0, text.Length, buf, 0);
            msg.Body = buf;
            msg.BodyType = BodyType.UnicodeString;
        }

        public static string BodyUTF16(this Message msg)
        {
            Contract.Requires(msg != null);
            Contract.Requires(msg.BodyType == BodyType.UnicodeString);

            var buf = msg.Body;
            if (buf == null) return null;
            var chars = buf.Length > 1 && buf[buf.Length - 1] == 0  && buf[buf.Length - 2] == 0 ? buf.Length - 2 : buf.Length; // remove trailing null
            return Encoding.Unicode.GetString(buf, 0, chars);
        }       

        public static void ExtensionUTF8(this Message msg, string text)
        {
            Contract.Requires(msg != null);
            Contract.Requires(text != null);

            msg.Extension = utf8NoBom.GetBytes(text);
        }

        public static string ExtensionUTF8(this Message msg)
        {
            Contract.Requires(msg != null);
            var buf = msg.Extension;
            return buf == null ? null : utf8NoBom.GetString(buf);
        }

        public static void ExtensionASCII(this Message msg, string text)
        {
            Contract.Requires(msg != null);
            Contract.Requires(text != null);

            var count = Encoding.ASCII.GetByteCount(text);
            var buf = new byte[count + 1]; // one extra for null char
            Encoding.ASCII.GetBytes(text, 0, text.Length, buf, 0);
            msg.Extension = buf;
        }

        public static string ExtensionASCII(this Message msg)
        {
            Contract.Requires(msg != null);

            var buf = msg.Extension;
            if (buf == null) return null;
            var chars = buf.Length > 0 && buf[buf.Length - 1] == 0 ? buf.Length - 1 : buf.Length; // remove trailing null
            return Encoding.ASCII.GetString(buf, 0, chars);
        }

        /// <summary>
        /// Uses a <see cref="QueueCursor"/> to look for messages with a matching <paramref name="correlationId"/>.
        /// Returns the matching message or NULL if no matching message can be found with the allowed <paramref name="timeout"/>.
        /// </summary>
        public static Message ReadByCorrelationId(this QueueReader queue, MessageId correlationId, Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            timeout = timeout ?? QueueReader.Infinite;
            var start = DateTime.UtcNow;
            using (var cur = new QueueCursor(queue))
            {
                var msg = cur.Peek(Properties.CorrelationId | Properties.LookupId, timeout);
                for(;;)
                {
                    if (msg == null)
                        return null;

                    if (msg.CorrelationId == correlationId)
                        return queue.Lookup(properties, msg.LookupId, LookupAction.ReceiveCurrent, TimeSpan.Zero, transaction);

                    var elapsed = DateTime.UtcNow - start;
                    var remaining = timeout - elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return null;

                    msg = cur.PeekNext(Properties.CorrelationId | Properties.LookupId, remaining);
                }
            }
        }

        /// <summary>
        /// Uses a <see cref="QueueCursor"/> to look for messages with a matching <paramref name="correlationId"/>
        /// Returns the matching message or NULL if no matching message can be found with the allowed <paramref name="timeout"/>.
        /// </summary>
        public static async Task<Message> ReadByCorrelationIdAsync(this QueueReader queue, MessageId correlationId, Properties properties = Properties.All, TimeSpan? timeout = null, QueueTransaction transaction = null)
        {
            timeout = timeout ?? QueueReader.Infinite;
            var start = DateTime.UtcNow;
            using (var cur = new QueueCursor(queue))
            {
                var msg = await cur.PeekAsync(Properties.CorrelationId | Properties.LookupId, timeout);
                for(;;)
                {
                    if (msg == null)
                        return null;

                    if (msg.CorrelationId == correlationId)
                        return queue.Lookup(properties, msg.LookupId, LookupAction.ReceiveCurrent, TimeSpan.Zero, transaction);

                    var elapsed = DateTime.UtcNow - start;
                    var remaining = timeout - elapsed;
                    if (remaining <= TimeSpan.Zero)
                        return null;

                    msg = await cur.PeekNextAsync(Properties.CorrelationId | Properties.LookupId, remaining);
                }
            }
        }
    }

}
