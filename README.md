# BusterWood.Msmq.Patterns

[![Nuget](https://img.shields.io/nuget/v/BusterWood.Msmq.svg)](https://www.nuget.org/packages/BusterWood.Msmq)

Easy to use [messaging patterns]([BusterWood.Msmq](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq.Patterns) for .NET built on [BusterWood.Msmq](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq).

# BusterWood.Msmq

A .NET library for MSMQ (Microsoft Message Queuing).

My motivation for creating this library is to create useable patterns for MSMQ (see above), but found `System.Messaging` to be
missing features from MSMQ 3.0, i.e. [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) and [poison message handling](https://msdn.microsoft.com/en-us/library/ms703179(v=vs.85).aspx) for transactional queues.

### Differences from System.Messaging

* BusterWood.MSMQ only fully supports private queues at the moment, some public queue message properties are missing
* Format names are pure MSMQ format names, _they do not accept the `FormatName:` prefix required by `System.Messaging`_.
* `QueueReader.Peek` _replaces `MessageQueue.Peek` method_.
* `QueueReader.Read` _replaces `MessageQueue.Receive` method_.
* `QueueWriter.Write` _replaces `MessageQueue.Send` method_.
* `QueueReader.Lookup` method _replaces `ReceiveByLookupId`_ and additionally supports lookup of first, last, previous and next messages.
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
