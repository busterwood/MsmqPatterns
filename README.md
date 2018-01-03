# BusterWood.Msmq.Patterns

[![Nuget](https://img.shields.io/nuget/v/BusterWood.Msmq.svg)](https://www.nuget.org/packages/BusterWood.Msmq)

Easy to use [messaging patterns](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq.Patterns) for .NET built on [BusterWood.Msmq](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq).

# BusterWood.Msmq

[A .NET library for MSMQ](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq) (Microsoft Message Queuing) with support for MSMQ 3.0+ features.

## Motivation

My motivation for creating this library is to create useable patterns for MSMQ (see above), but found `System.Messaging` to be
missing features from MSMQ 3.0, i.e. [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) and [poison message handling](https://msdn.microsoft.com/en-us/library/ms703179(v=vs.85).aspx) for transactional queues.
