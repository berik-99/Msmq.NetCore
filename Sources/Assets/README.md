# Msmq.NetCore

[![NuGet](https://img.shields.io/nuget/v/Msmq.NetCore.svg)](https://www.nuget.org/packages/Msmq.NetCore)

**Msmq.NetCore** is a drop-in replacement for `System.Messaging` on .NET Core, providing access to the features of MSMQ on the latest .NET Core runtime. This library is a fork of the original `MSMQ.Messaging` project and has been updated to support .NET Standard 2.1.

> **Note**: This project is intended for use with .NET Core only. For .NET Framework users, it is recommended to continue using `System.Messaging`.

## Features

- **MSMQ Support**: Provides an interface for working with Microsoft Message Queuing (MSMQ) on .NET Core.
- **Fork of MSMQ.Messaging**: Built on the foundation of the original project, updated for modern .NET technologies.
- **Simple Integration**: Drop-in replacement for the legacy `System.Messaging` namespace, making it easy to migrate to .NET Core.

## Usage

### Basic Example

To start using `Msmq.NetCore`, you can replace `System.Messaging` with `Msmq.NetCore` in your project. Here's an example of how to send and receive messages:

```
using Msmq.NetCore;

var queuePath = @".\Private$\MyQueue";

// Create the queue if it doesn't exist
if (!MessageQueue.Exists(queuePath))
{
    MessageQueue.Create(queuePath);
}

// Sending a message
using (var queue = new MessageQueue(queuePath))
{
    var message = new Message("Hello, MSMQ!");
    queue.Send(message);
}

// Receiving a message
using (var queue = new MessageQueue(queuePath))
{
    queue.Formatter = new XmlMessageFormatter(new string[] { "System.String" });
    var receivedMessage = queue.Receive();
    Console.WriteLine($"Received message: {receivedMessage.Body}");
}
```

### Compatibility

- **Target Framework**: .NET Standard 2.1 (only for .NET Core).
- **Platform**: Windows (MSMQ is platform-specific).

## Contributing

Contributions are welcome! If you'd like to contribute to `Msmq.NetCore`, please fork the repository and submit a pull request. 

We ask that you respect the project's goal of staying as close as possible to the reference implementation of MSMQ, with the aim of providing a simple migration path to .NET Core.

## Acknowledgments

This project is a **fork** of [MSMQ.Messaging](https://github.com/weloytty/MSMQ.Messaging), created by Bill Loytty. We would like to thank him for laying the groundwork for this project.

## License

This project is licensed under the [MIT License](../LICENSE).
