using System;
namespace BusterWood.Msmq
{
    /// <summary>The properties to read when receiving a message</summary>
    [Flags]
    public enum Properties
    {
        Class = 1,
        AcknowledgementTypes = 1 << 1,
        AdministrationQueue = 1 << 2,
        AppSpecific = 1 << 3,
        Body = 1 << 4,
        CorrelationId = 1 << 5,
        Delivery = 1 << 6,
        Extension = 1 << 7,
        Id = 1 << 8,
        Label = 1 << 9,
        LookupId = 1 << 10,
        Priority = 1 << 11,
        ResponseQueue = 1 << 12,
        DestinationQueue = 1 << 13,
        SentTime = 1 << 14,
        TimeToBeReceived = 1 << 15,
        TimeToReachQueue = 1 << 16,
        ArrivedTime = 1 << 17,
        Journal = 1 << 18,
        SourceMachine = 1 << 19,
        TransactionId = 1 << 20,
        TransactionAbortCount = 1 << 21,
        TransactionMoveCount = 1 << 22,
        TransactionFirst = 1 << 23,
        TransactionLast = 1 << 24,

        /// <summary>Read all properties including the body</summary>
        All = Class| AcknowledgementTypes | AdministrationQueue | AppSpecific | Body | CorrelationId | Delivery 
            | DestinationQueue | Extension | Id | Label | LookupId | Priority | ResponseQueue | SentTime
            | TimeToBeReceived | TimeToReachQueue | ArrivedTime | Journal | SourceMachine | TransactionId 
            | TransactionAbortCount | TransactionMoveCount | TransactionFirst | TransactionLast,
    }
}