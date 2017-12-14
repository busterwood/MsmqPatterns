using System;

namespace BusterWood.Msmq
{
    /// <summary>The identifier of the message, a Guid and int64</summary>
    public struct MessageId : IEquatable<MessageId>
    {
        /// <summary>An empty message id</summary>
        public static readonly MessageId None = new MessageId();

        internal const int GenericIdSize = 16;
        internal const int MessageIdSize = 20;

        internal readonly byte[] bytes;

        public MessageId(byte[] id)
        {
            bytes = id;
            if (bytes.Length != MessageIdSize)
                Array.Resize(ref bytes, MessageIdSize);
        }

        /// <summary>Is this is null or all zeros?</summary>
        public bool IsNullOrEmpty()
        {
            if (bytes == null) return true;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != 0)
                    return false;
            }
            return true;
        }

        /// <summary>Returns Guid\long </summary>
        public override string ToString()
        {
            if (IsNullOrEmpty()) return "";

            byte[] guidBytes = new byte[GenericIdSize];
            Array.Copy(bytes, guidBytes, GenericIdSize);
            int id = BitConverter.ToInt32(bytes, GenericIdSize);
            var guid = new Guid(guidBytes);
            return $"{guid}\\{id}";
        }

        /// <summary>Does this id have the guid and int64 value as another one?</summary>
        public bool Equals(MessageId other)
        {
            if (IsNullOrEmpty()) return other.IsNullOrEmpty();
            if (other.IsNullOrEmpty()) return false;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != other.bytes[i])
                    return false;
            }
            return true;
        }

        /// <summary>Does this id have the guid and int64 value as another one?</summary>
        public override bool Equals(object obj) => obj is MessageId && Equals((MessageId)obj);

        /// <summary>Returns the hash code of this id, or zero when this is <see cref="IsNullOrEmpty"/></summary>
        public override int GetHashCode()
        {
            if (bytes == null) return 0;
            int hc = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                unchecked
                {
                    hc += bytes[i] * 12345; // if all bytes are zero the hash code will be zero
                }
            }
            return hc;
        }

        public static bool operator==(MessageId left, MessageId right) => left.Equals(right);

        public static bool operator!=(MessageId left, MessageId right) => !left.Equals(right);

        /// <summary>Tries to parse the string for of the an id</summary>
        public static MessageId Parse(string id)
        {
            string[] bits = id.Split('\\');
            if (bits.Length != 2)
                throw new ArgumentException("Id is invalid");

            Guid guid;
            if (!Guid.TryParse(bits[0], out guid))
                throw new ArgumentException("Guid of Id is invalid");

            int intId;
            if (!int.TryParse(bits[1], out intId))
                throw new ArgumentException("Id is invalid");

            byte[] bytes = new byte[MessageIdSize];
            Array.Copy(guid.ToByteArray(), bytes, GenericIdSize);
            Array.Copy(BitConverter.GetBytes(intId), 0, bytes, GenericIdSize, 4);
            return new MessageId(bytes);
        }

    }
}
