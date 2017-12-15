# BusterWood.MSMQ

A .NET library for MSMQ (Microsoft Message Queuing).

My motivation for creating this library is to create useable components for MSMQ (see BusterWood.MsmqPatterns below), but found `System.Messaging` to be
missing features from MSMQ 3.0, i.e. sub-queues.

### Feature summary

New a `QueueReader` to peek or read messages from a queue.

New a `QueueWriter` to send messages to a queue.

New a `SubQueue` to peek or read message, and to move messages via `Queue.MoveMessage` method.

New a `Cursor` to peek or receive messages using a MSMQ cursor.

### Differences from System.Messaging

* Direct support for Task via `PeekAsync()` and `ReadAsync()` methods.
* You can only open queues using format names.  Use `Queue.TryCreate()` or `Queue.PathToFormatName()` to get a format name from a queue path.
* Additional message properties are supported, e.g. `TransactionFirst`, `TransactionLast`, `TransactionAbortCount`, `TransactionMoveCount`.
* Message properties for `AdministrationQueue`, `DestinationQueue` and `ResponseQueue` are format names, not type `MessageQueue`.
* Message `Id` and `CorrelationId` properties have a type of `MessageId`, not string.
