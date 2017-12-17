# BusterWood.MSMQ

A .NET library for MSMQ (Microsoft Message Queuing).

My motivation for creating this library is to create useable components for MSMQ (see BusterWood.MsmqPatterns below), but found `System.Messaging` to be
missing features from MSMQ 3.0, i.e. [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx).

### Feature summary

New a `QueueReader` to peek or read messages from a queue.

New a `QueueWriter` to send messages to a queue.

New a `SubQueue` to peek or read message, and to move messages to [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) via `Queue.MoveMessage` method.

New a `Cursor` to peek or receive messages using a MSMQ cursor.

New a `QueueTransaction` to begin a MSMQ transaction, or use the static `QueueTransaction.Single` or `QueueTransaction.Dtc` fields.

Supports poison message handling via the `TransactionAbortCount` and `TransactionMoveCount`properties and the `MarkRejected` method that sends notification to the message sender that the message was rejected.

### Differences from System.Messaging

* BusterWood.MSMQ only fully supports private queues at the moment, some public queue message properties are missing
* Direct support for Task via `PeekAsync()` and `ReadAsync()` methods.
* You can only open queues using format names.  Use `Queue.TryCreate()` or `Queue.PathToFormatName()` to get a format name from a queue path.
* Additional message properties are supported, e.g. `TransactionFirst`, `TransactionLast`, `TransactionAbortCount`, `TransactionMoveCount`.
* Message properties for `AdministrationQueue`, `DestinationQueue` and `ResponseQueue` are format names, not type `MessageQueue`.
* Message `Id` and `CorrelationId` properties have a type of `struct MessageId`, not string.
* `Body` has a type of `byte[]`, and is either a byte array, ACSII or UTF-16 string
* All the `Read..` and `Peek...` methods accept a `Properties` parameter (default is `All`), which replaces the `Message.MessageReadFilter` 
* The `QueueTransaction` class automatically starts a transaction when it is created, and replaces `MessageQueueTransaction` class
* `MessageQueueTransactionType` enum is replaced with the static `QueueTransaction.Single` or `QueueTransaction.Dtc` fields
* Support for rejecting messages via the `MarkRejected` method (see [MQMarkMessageRejected](https://msdn.microsoft.com/en-us/library/ms707071(v=vs.85).aspx)).

# BusterWood.MsmqPatterns

Reuseable messaging patterns for MSMQ.

## Summary

* Start a `Postman` to send messages with confirmation the message was delivered to the destination queue or get and error if it cannot be delivered.
* Send requests and wait for reply with the `RequestReply`class (uses the `Postman` to confirm delivery and processing)
* Route messages to [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) with the `SubQueueFilterRouter` class
* Route batches of messages between transactional queues with the `TransactionalRouter` class (uses the `Postman` to confirm delivery)
* Route messages between non-transactional queues with the `NonTransactionalRouter` class
