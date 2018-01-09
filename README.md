# BusterWood.Msmq.Patterns

[![Nuget](https://img.shields.io/nuget/v/BusterWood.Msmq.svg)](https://www.nuget.org/packages/BusterWood.Msmq)

Easy to use patterns for .NET and MSMQ built on [BusterWood.Msmq](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq). e.g. publish/subscribe messaging using message labels, request/reply pattern, sending with delivery notification.

Please see the [wiki page](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq.Patterns) for more details.

# BusterWood.Msmq

A .NET library for MSMQ (Microsoft Message Queuing) with support for MSMQ 3.0+ features, i.e. [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx) and [poison message handling](https://msdn.microsoft.com/en-us/library/ms703179(v=vs.85).aspx) for transactional queues. 

Please read the [wiki page](https://github.com/busterwood/MsmqPatterns/wiki/BusterWood.Msmq) for more details.

## Why?

To create reliable patterns for MSMQ I needed transactional support for moving messages to [subqueues](https://msdn.microsoft.com/en-us/library/ms711414(v=vs.85).aspx), but this was not supported by `System.Messaging`.
