using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Busterwood.Msmq
{
    public class Message
    {
        internal readonly MessageProperties Props;

        internal const int GenericIdSize = 16;
        internal const int MessageIdSize = 20;

        public Message() : this(new MessageProperties())
        {
            // always add Id so we can read it after send
            Props.SetByteArray(Native.MESSAGE_PROPID_MSGID, new byte[MessageIdSize]);
        }

        internal Message(MessageProperties properties)
        {
            Contract.Requires(properties != null);
            Props = properties;
        }     

        public string Id
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_MSGID))
                    return "";
                var bytes = Props.GetByteArray(Native.MESSAGE_PROPID_MSGID);
                return bytes == null ? "" : IdFromByteArray(bytes);
            }
        }

        private string IdFromByteArray(byte[] bytes)
        {
            StringBuilder result = new StringBuilder();
            byte[] guidBytes = new byte[GenericIdSize];
            Array.Copy(bytes, guidBytes, GenericIdSize);
            int id = BitConverter.ToInt32(bytes, GenericIdSize);
            result.Append((new Guid(guidBytes)).ToString());
            result.Append("\\");
            result.Append(id);
            return result.ToString();
        }

        public AcknowledgmentTypes AcknowledgementTypes
        {
            get
            {
                return (AcknowledgmentTypes)Props.GetByte(Native.MESSAGE_PROPID_ACKNOWLEDGE);
            }
            set
            {
                if (value == AcknowledgmentTypes.None)
                {
                    Props.Remove(Native.MESSAGE_PROPID_ACKNOWLEDGE);
                }
                else
                {
                    Props.SetByte(Native.MESSAGE_PROPID_ACKNOWLEDGE, (byte)value);
                }
            }
        }

        /// <summary>Gets or sets the format name of the queue that administration messages are sent too</summary>
        public string AdministrationQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_ADMIN_QUEUE))
                    return null;

                var len = Props.GetUInt(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN);
                return len == 0 ? null : StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_ADMIN_QUEUE), len);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Props.Remove(Native.MESSAGE_PROPID_ADMIN_QUEUE);
                    Props.Remove(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN);
                }
                else
                {
                    Props.SetString(Native.MESSAGE_PROPID_ADMIN_QUEUE, StringToBytes(value));
                    Props.SetUInt(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN, value.Length);
                }
            }
        }

        public int AppSpecific
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_APPSPECIFIC))
                    return 0;
                return Props.GetUInt(Native.MESSAGE_PROPID_APPSPECIFIC);
            }
            set
            {
                if (value == 0)
                    Props.Remove(Native.MESSAGE_PROPID_APPSPECIFIC);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_APPSPECIFIC, value);
            }
        }

        public DateTime? ArrivedTime
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_ARRIVEDTIME))
                    return null;

                DateTime time = new DateTime(1970, 1, 1);
                time = time.AddSeconds(Props.GetUInt(Native.MESSAGE_PROPID_ARRIVEDTIME)).ToLocalTime();
                return time;
            }
        }
        
        public byte[] Body
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_BODY))
                    return null;
                var len = Props.GetUInt(Native.MESSAGE_PROPID_BODY_SIZE);
                if (len == 0)
                    return null;
                var data = Props.GetByteArray(Native.MESSAGE_PROPID_BODY);
                var retVal = new byte[len];
                Array.Copy(data, retVal, len);
                return retVal;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                {
                    Props.Remove(Native.MESSAGE_PROPID_BODY);
                    Props.Remove(Native.MESSAGE_PROPID_BODY_SIZE);
                }
                else
                {
                    Props.SetByteArray(Native.MESSAGE_PROPID_BODY, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_BODY_SIZE, value.Length);
                }
            }
        }

        public BodyType BodyType
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_BODY_TYPE))
                    return 0;
                return (BodyType)Props.GetUInt(Native.MESSAGE_PROPID_BODY_TYPE);
            }
            set
            {
                Props.SetUInt(Native.MESSAGE_PROPID_BODY_TYPE, (int)value);
            }
        }

        public string CorrelationId
        {
            get
            {
                var id = Props.GetByteArray(Native.MESSAGE_PROPID_CORRELATIONID);
                return id.Any(b => b != 0) ? IdFromByteArray(id) : "";
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                    Props.Remove(Native.MESSAGE_PROPID_CORRELATIONID);
                else
                    Props.SetByteArray(Native.MESSAGE_PROPID_CORRELATIONID, IdToByteArray(value));
            }
        }

        private byte[] IdToByteArray(string id)
        {
            string[] bits = id.Split('\\');
            if (bits.Length != 2)
                throw new InvalidOperationException("Id is invalid");
            
            Guid guid;
            if (!Guid.TryParse(bits[0], out guid))
                throw new InvalidOperationException("Guid of Id is invalid");

            int intId;
            if (!int.TryParse(bits[1], out intId))
                throw new InvalidOperationException("Id is invalid");

            byte[] bytes = new byte[MessageIdSize];
            Array.Copy(guid.ToByteArray(), bytes, GenericIdSize);
            Array.Copy(BitConverter.GetBytes(intId), 0, bytes, GenericIdSize, 4);
            return bytes;
        }

        /// <summary>Is this an <see cref="Busterwood.Msmq.Delivery.Express"/> or <see cref="Busterwood.Msmq.Delivery.Recoverable"/> message?</summary>
        public Delivery Delivery
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_DELIVERY))
                    return Delivery.Express;
                return (Delivery)Props.GetByte(Native.MESSAGE_PROPID_DELIVERY);
            }
            set
            {
                if (value == Delivery.Express)
                    Props.Remove(Native.MESSAGE_PROPID_DELIVERY);
                else
                    Props.SetByte(Native.MESSAGE_PROPID_DELIVERY, (byte)Delivery.Recoverable);
            }
        }

        /// <summary>Gets format name of the original destination queue.  Returns empty string when message has not been sent.</summary>
        public string DestinationQueue
        {
            get
            {
                var len = Props.GetUInt(Native.MESSAGE_PROPID_DEST_QUEUE_LEN);
                return len == 0 ? "" : StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_DEST_QUEUE), len);
            }
        }

        public byte[] Extension
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_EXTENSION))
                    return null;
                var len = Props.GetUInt(Native.MESSAGE_PROPID_EXTENSION_LEN);
                if (len == 0)
                    return null;
                var data = Props.GetByteArray(Native.MESSAGE_PROPID_EXTENSION);
                var retVal = new byte[len];
                Array.Copy(data, retVal, len);
                return retVal;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                {
                    Props.Remove(Native.MESSAGE_PROPID_EXTENSION);
                    Props.Remove(Native.MESSAGE_PROPID_EXTENSION_LEN);
                }
                else
                {
                    Props.SetByteArray(Native.MESSAGE_PROPID_EXTENSION, value);
                    Props.SetUInt(Native.MESSAGE_PROPID_EXTENSION_LEN, value.Length);
                }
            }
        }
        /// <summary>
        /// <see cref="Msmq.Journal.Journal"/> the message and/or use the <see cref="Msmq.Journal.DeadLetter"/> queue?
        /// </summary>
        public Journal Journal
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_JOURNAL))
                    return Journal.None;
                return (Journal)Props.GetByte(Native.MESSAGE_PROPID_JOURNAL);
            }
            set
            {
                if (value == Journal.None)
                    Props.Remove(Native.MESSAGE_PROPID_JOURNAL);
                else
                    Props.SetByte(Native.MESSAGE_PROPID_JOURNAL, (byte)value);
            }
        }

        /// <summary>Gets or sets the label of the message, much like the subject of an e-mail.</summary>
        public string Label
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_LABEL_LEN))
                    return "";
                var labelLen = Props.GetUInt(Native.MESSAGE_PROPID_LABEL_LEN);
                return labelLen == 0 ? "" : StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_LABEL), labelLen);
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (value.Length == 0)
                {
                    Props.Remove(Native.MESSAGE_PROPID_LABEL);
                    Props.Remove(Native.MESSAGE_PROPID_LABEL_LEN);
                }
                else
                {
                    Props.SetString(Native.MESSAGE_PROPID_LABEL, StringToBytes(value));
                    Props.SetUInt(Native.MESSAGE_PROPID_LABEL_LEN, value.Length);
                }
            }
        }

        static string StringFromBytes(byte[] bytes, int len)
        {
            //trim the last null char
            if (len != 0 && bytes[len * 2 - 1] == 0 && bytes[len * 2 - 2] == 0)
                --len;

            char[] charBuffer = new char[len];
            Encoding.Unicode.GetChars(bytes, 0, len * 2, charBuffer, 0);
            return new string(charBuffer, 0, len);
        }

        static byte[] StringToBytes(string value)
        {
            byte[] byteBuffer = new byte[(value.Length * 2 + 1)]; // one more for null
            Encoding.Unicode.GetBytes(value.ToCharArray(), 0, value.Length, byteBuffer, 0);
            return byteBuffer;
        }

        /// <summary>The queue specific ID of this message.  This property is set once the message has been sent.</summary>
        public long LookupId
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_LOOKUPID))
                    return 0;
                return Props.GetULong(Native.MESSAGE_PROPID_LOOKUPID);
            }
        }

        public Priority Priority
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_PRIORITY))
                    return Priority.Normal;
                return (Priority)Props.GetByte(Native.MESSAGE_PROPID_PRIORITY);
            }
            set
            {
                if (value == Priority.Normal)
                    Props.Remove(Native.MESSAGE_PROPID_PRIORITY);
                else
                    Props.SetByte(Native.MESSAGE_PROPID_PRIORITY, (byte)value);
            }
        }

        /// <summary>Gets or sets the format name of the response queue</summary>
        public string ResponseQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_RESP_QUEUE))
                    return null;
                var len = Props.GetUInt(Native.MESSAGE_PROPID_RESP_QUEUE_LEN);
                return len == 0 ? "" : StringFromBytes(Props.GetString(Native.MESSAGE_PROPID_RESP_QUEUE), len);
            }
            set
            {
                if (value == null)
                {
                    Props.Remove(Native.MESSAGE_PROPID_RESP_QUEUE);
                    Props.Remove(Native.MESSAGE_PROPID_RESP_QUEUE_LEN);
                }
                else
                {
                    Props.SetString(Native.MESSAGE_PROPID_RESP_QUEUE, StringToBytes(value));
                    Props.SetUInt(Native.MESSAGE_PROPID_RESP_QUEUE_LEN, value.Length);
                }
            }
        }

        public DateTime? SentTime
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_SENTTIME))
                    return null;

                DateTime time = new DateTime(1970, 1, 1);
                time = time.AddSeconds(Props.GetUInt(Native.MESSAGE_PROPID_SENTTIME)).ToLocalTime();
                return time;
            }
        }

        public TimeSpan TimeToBeReceived
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED))
                    return Queue.Infinite;

                return TimeSpan.FromSeconds((uint)Props.GetUInt(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED));
            }
            set
            {
                long timeoutInSeconds = (long)value.TotalSeconds;
                if (timeoutInSeconds < 0)
                    throw new ArgumentException("Cannot be negative", nameof(value));

                if (timeoutInSeconds >= uint.MaxValue)
                    Props.Remove(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED, (int)((uint)timeoutInSeconds));
            }
        }

        public TimeSpan TimeToReachQueue
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE))
                    return Queue.Infinite;

                return TimeSpan.FromSeconds((uint)Props.GetUInt(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE));
            }
            set
            {
                long timeoutInSeconds = (long)value.TotalSeconds;
                if (timeoutInSeconds < 0)
                    throw new ArgumentException("Cannot be negative", nameof(value));

                if (timeoutInSeconds >= uint.MaxValue)
                    Props.Remove(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE);
                else
                    Props.SetUInt(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE, (int)((uint)timeoutInSeconds));
            }
        }

        public MessageClass Class
        {
            get
            {
                if (Props.IsUndefined(Native.MESSAGE_PROPID_CLASS))
                    return MessageClass.Normal;

                return (MessageClass)Props.GetUShort(Native.MESSAGE_PROPID_CLASS);
            }
        }
    }
}