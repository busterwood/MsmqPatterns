﻿using System;
using System.Diagnostics.Contracts;

namespace BusterWood.Msmq
{
    [Flags]
    public enum MessageClass
    {
        /// <summary>
        /// A normal Message Queuing message
        /// </summary>
        Normal = 0x0000,

        /// <summary>
        /// Message class sent by MSMQ to <see cref="Message.AdministrationQueue"/>
        /// </summary>
        Report = 0x0001,

        /// <summary>
        /// The message was delivered to its destination queue
        /// </summary>
        ReachQueue = 0x0002,

        OrderAck = 0x00ff,

        /// <summary>
        /// The message was retrieved by the receiving application
        /// </summary>
        Received = 0x4000,

        /// <summary>
        /// The destination queue is not available to the sending application
        /// </summary>
        BadDestinationQueue = 0x8000,

        /// <summary>
        /// The queue was deleted before reaching the destination queue
        /// </summary>
        Deleted = 0x8001,

        /// <summary>
        /// the message did not reach the destination queue. 
        /// This message can be generated by expiration of either the <see cref="Message.TimeToBeReceived"/> time or <see cref="Message.TimeToReachQueue"/> time before the message reaches the destination queue.
        /// </summary>
        ReachQueueTimeout = 0x8002,

        /// <summary>
        /// the message was rejected by the destination queue manager because the destination queue exceeded Quota.
        /// </summary>
        QueueExceedQuota = 0x8003,

        /// <summary>
        /// The access rights for placing messages in the destination queue are not allowed for the sending application.
        /// </summary>
        AccessDenied = 0x8004,

        /// <summary>
        /// The hop count of the original message is exceeded
        /// </summary>
        HopCountExceeded = 0x8005,

        BadSignature = 0x8006,

        BadEncryption = 0x8007,

        /// <summary>
        /// A transactional message was sent to a non-transactional queue
        /// </summary>
        NotTransactionalQueue = 0x8009,

        /// <summary>
        /// A non-transactional message was sent to a transactional queue.
        /// </summary>
        NotTransactionalMessage = 0x800A,

        /// <summary>
        /// Unsupported crypto provider
        /// </summary>
        CouldNotEncrypt = 0x800B,

        /// <summary>
        /// The queue was deleted before the original message could be read from the queue
        /// </summary>
        QueueDeleted = 0xC000,

        /// <summary>
        /// The queue was purged and the original message no longer exists
        /// </summary>
        QueuePurged = 0xC001,        

        /// <summary>
        /// The message was placed in the destination queue but was not retrieved from the queue before its time-to-be-received timer expired.
        /// </summary>
        ReceiveTimeout = 0xC002,

        /// <summary>
        /// The message was rejected by a receiving application. Note the message was consumed from the queue but could not be processed.
        /// </summary>
        ReceiveRejected = 0xC004,
    }

    public partial class Extensions
    {
        public static MessageClass Acknowledgement(this Message msg)
        {
            Contract.Requires(msg != null);
            return msg.Class.Acknowledgement();
        }

        public static MessageClass Acknowledgement(this MessageClass @class)
        {
            return @class & (MessageClass)ushort.MaxValue & ~(MessageClass.Normal  | MessageClass.Report);
        }

        public static MessageClassSeverity Severity(this MessageClass @class) => (MessageClassSeverity)@class & MessageClassSeverity.Negative;

        public static MessageClassReceive Receive(this MessageClass @class) => (MessageClassReceive)@class & MessageClassReceive.Receive;

        public static short ClassCode(this MessageClass @class) => (short)((int)@class & 1023);
    }

    public enum MessageClassSeverity
    {
        Normal = 0,
        Negative = 1 << 15,
    }

    public enum MessageClassReceive
    {
        Arrival = 0,
        Receive = 1 << 14,
    }

}