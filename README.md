# BusterWood.MSMQ

A .NET library for MSMQ (Microsoft Message Queuing).

My motivation for creating this library is to create useable components for MSMQ (see BusterWood.Msmq.Patterns below), but found `System.Messaging` to be
missing features from MSMQ 3.0, i.e. [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) and [poison message handling](https://msdn.microsoft.com/en-us/library/ms703179(v=vs.85).aspx) for transactional queues.

### Feature summary

New a `QueueReader` to peek or read messages from a queue.

New a `QueueWriter` to send messages to a queue.

New a `SubQueue` to peek or read message, and to move messages to [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) via `Queue.MoveMessage` method.

New a `QueueCursor` to peek or receive messages using a MSMQ cursor.

New a `QueueTransaction` to begin a MSMQ transaction, or use the static fields `QueueTransaction.None`, `QueueTransaction.Single` or `QueueTransaction.Dtc`.

Supports [poison message handling for transactional queues](https://msdn.microsoft.com/en-us/library/ms703179(v=vs.85).aspx) via the `TransactionAbortCount` and `TransactionMoveCount`properties and the `MarkRejected` method that sends notification to the message sender that the message was rejected.

### Differences from System.Messaging

* BusterWood.MSMQ only fully supports private queues at the moment, some public queue message properties are missing
* Format names are pure MSMQ format names, _they do not accept the `FormatName:` prefix required by `System.Messaging`_.
* Direct support for Tasks and `async/await` via `PeekAsync()` and `ReadAsync()` methods.
* Methods that accept a timeout, e.g. `Peek...()` and `Read...()` methods, return `null` if the timeout was reached _rather than throwing an exception_.
* You can only open queues using format names.  Use `Queue.TryCreate()` or `Queue.PathToFormatName()` to get a format name from a queue path.
* Message properties for `AdministrationQueue`, `DestinationQueue` and `ResponseQueue` are format names, _not type `MessageQueue`_.
* Message `Id` and `CorrelationId` properties have a type of `struct MessageId`, not string.
* `Body` has a type of `byte[]`, and is either a byte array, ACSII or UTF-16 string
* All the `Read..` and `Peek...` methods accept a `Properties` parameter (default is `All`), _which replaces the `Message.MessageReadFilter`_.
* The `QueueTransaction` class automatically starts a transaction when it is created, _and replaces `MessageQueueTransaction` class_.
* `QueueTransaction.None`, `QueueTransaction.Single` or `QueueTransaction.Dtc` static fields replace _`MessageQueueTransactionType` enum_. 
* Additional message properties are supported, e.g. `TransactionFirst`, `TransactionLast`, `TransactionAbortCount`, `TransactionMoveCount`.
* `QueueReader.Lookup` method _replaces `ReceiveByLookupId`_ and additionally supports lookup of first, last, previous and next messages.

# BusterWood.Msmq.Patterns

Reuseable messaging patterns for MSMQ built on `BusterWood.MSMQ`.

## Summary

The core pattern is the `Postman`, which supports sending messages with confirmation of delivery to the destination queue (and receiving), _or_ an error if it cannot be delivered or was not received in time.

The following patterns then use the `Postman`:
* Send requests and wait for reply with the `RequestReply`class
* Route batches of messages between transactional queues with the `TransactionalRouter` class
* Route messages between non-transactional queues with the `NonTransactionalRouter` class

The following patterns do not use `Postman`:
* Route messages to [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) with the `SubQueueFilterRouter` class

All classes are `async` code bulit on `System.Threading.Tasks.Task`.
