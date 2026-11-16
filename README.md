# Msmq.NetCore

![Msmq.NetCore icon](https://raw.githubusercontent.com/berik-99/Msmq.NetCore/main/icon.png)

Msmq.NetCore provides MSMQ access for .NET Core and modern .NET on Windows. It is a port of the System.Messaging API, forked from MSMQ.Messaging.

**For .NET Framework applications, use Microsoft's official `System.Messaging` assembly.** This package focuses on .NET Core and modern .NET.

## Release 1.2.0

This release adds a dedicated .NET 11 target, restores the .NET Standard 2.1 fallback for .NET Core 3.0 and later, and includes external IntelliSense XML documentation for every target. It fixes message buffer corruption, direct ActiveX Write/Send, partial stream reads, pinned buffer leaks, formatter state, and connection cache cleanup.

When upgrading from 1.1.x or earlier, review the [differences from System.Messaging](#differences-from-systemmessaging): ActiveX `VT_I1` now returns `sbyte`, and changing XML target types clears cached serializers. Applications using `BinaryMessageFormatter` must explicitly enable binary serialization; on .NET 9+, they must also reference the compatibility package described [below](#binary-compatibility). The library no longer provides that package or enables binary serialization on behalf of the application.

## Compatibility

| Application runtime | Library asset |
| --- | --- |
| .NET Core 3.0, 3.1 | `netstandard2.1` |
| .NET 5, 6, 7 | `netstandard2.1` |
| .NET 8, 9, 10, 11 | Dedicated target for each version |
| .NET Framework | Incompatible with this package; use `System.Messaging` |

MSMQ must be installed and running on Windows. .NET Core 1.x and 2.x are not supported. The .NET Standard 2.1 fallback excludes .NET Framework from NuGet compatibility. Compatibility with an older runtime does not extend that runtime's Microsoft support lifecycle. Distributed transactions depend on the capabilities of the application runtime and Windows/MSDTC; they are not guaranteed by the .NET Standard target.

## Differences from System.Messaging

The main namespace is `Msmq.NetCore.Messaging`. The port keeps the public signatures, parameter names, and constants of the corresponding System.Messaging types. Compatibility is not a promise of identical behavior in every framework feature or deployment mode.

| Area | System.Messaging on .NET Framework | This port |
| --- | --- | --- |
| ActiveX `VT_I1` containing `FF` | `Read` returns `byte` 255 | `Read` returns `sbyte` -1, preserving the signed variant |
| Replacing XML `TargetTypes` / `TargetTypeNames` after first use | Previously cached serializers remain | Previous serializers are discarded and rebuilt from the current settings |
| Truncated ActiveX primitive streams | May produce incomplete values | Throws `EndOfStreamException`; partial reads are completed before decoding |
| Code Access Security | Framework security API | Types retained for compatibility; Windows queue ACLs enforce access |
| BinaryFormatter | Framework implementation | Obsolete, explicit application opt-in; compatibility package required on .NET 9+ |
| Installer and design tools | `MessageQueueInstaller`, `Design.QueuePathDialog`, `Design.QueuePathEditor` | Not provided; create/configure queues through the runtime API or Windows administration tools |

Calling `ActiveXMessageFormatter.Write(message, value)` directly before `queue.Send(message)` preserves the serialized ActiveX body. It does not require setting `message.Formatter` and does not silently convert the payload to XML.

The signed-byte and XML-cache differences are intentional corrections. Existing callers that cast `VT_I1` to `byte`, or rely on serializers retained after changing target types, must adapt their code. XML target settings are serializer configuration, not a security boundary for untrusted input.

## Installation

Install the [Msmq.NetCore NuGet package](https://www.nuget.org/packages/Msmq.NetCore):

```powershell
dotnet add package Msmq.NetCore
```

The namespace is `Msmq.NetCore.Messaging`. When migrating a .NET Framework application to .NET Core or modern .NET, replace its `System.Messaging` reference and using directive with this package and namespace.

## Example

```csharp
using System;
using Msmq.NetCore.Messaging;

string path = @".\private$\my-queue";
if (!MessageQueue.Exists(path))
{
    using (var created = MessageQueue.Create(path)) { }
}

using (var queue = new MessageQueue(path))
{
    queue.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
    queue.Send("Hello, MSMQ!");
    using (var received = queue.Receive(TimeSpan.FromSeconds(5)))
        Console.WriteLine(received.Body);
}
```

The queue in this example is nontransactional. Use an explicit `MessageQueueTransaction` or `MessageQueueTransactionType.Single` when sending to or receiving from a transactional queue.

## Binary compatibility

XML is the default formatter. `ActiveXMessageFormatter` supports the existing MSMQ primitive formats. `BinaryMessageFormatter` remains available and obsolete for legacy message compatibility.

BinaryFormatter has security vulnerabilities. Never deserialize binary messages from untrusted senders. From .NET 9 onward, an application that explicitly needs binary compatibility must reference the unsupported compatibility package and enable serialization **in the executable project**, not just in a library:

```xml
<PropertyGroup>
  <EnableUnsafeBinaryFormatterSerialization>true</EnableUnsafeBinaryFormatterSerialization>
</PropertyGroup>
<ItemGroup>
  <!-- Version shown for an application targeting .NET 11. -->
  <PackageReference Include="System.Runtime.Serialization.Formatters" Version="11.0.0" />
</ItemGroup>
```

See Microsoft's [BinaryFormatter compatibility guidance](https://learn.microsoft.com/dotnet/standard/serialization/binaryformatter-migration-guide/compatibility-package). The library does not enable this application setting automatically. The legacy Code Access Security types are retained for API compatibility; Windows queue ACLs enforce access.

## Build and test

Building the complete solution requires the .NET 11 SDK. Integration tests require Windows, MSMQ, and permission to create/delete local private queues.

```powershell
dotnet build Msmq.NetCore.sln -c Release
dotnet test --project Tests/Msmq.NetCore.Test/Msmq.NetCore.Test.csproj
```

The tests target .NET 8 through 11 and use xUnit v3 and Microsoft.Testing.Platform, selected in `global.json`. Each test instance owns unique private queues. Binary compatibility is explicitly enabled only in the test executable.

The console project runs XML, ActiveX, asynchronous receive, and transaction checks on the selected installed runtime. For example:

```powershell
dotnet run --project Tests/Msmq.NetCore.CosnoleTest -f netcoreapp3.1
dotnet run --project Tests/Msmq.NetCore.CosnoleTest -f net11.0
```

Its targets cover .NET Core 3.1 and .NET 5 through 11. Running a target requires that runtime to be installed; a successful build alone does not certify every runtime or deployment mode.

To create the NuGet package:

```powershell
dotnet pack Sources/Msmq.NetCore.csproj -c Release
```

## External API documentation

IntelliSense documentation is supplied in `Msmq.NetCore.xml`, beside each DLL in the build output and each `lib/<framework>/` folder of the NuGet package. No source-code documentation comments or .NET Framework installation are required to build or consume it.

The checked-in file contains 666 entries adapted from Microsoft's API documentation, with namespace mappings and notes for this port's platform requirements and behavioral differences. Automated tests verify the member IDs, summaries, and output copy against the actual public API.

The source snapshot, attribution, transformation details, and regeneration command are recorded in `Sources/Documentation/ThirdPartyNotices.txt`. Regeneration uses `python tools/update_api_docs.py` and requires network access; normal builds use the checked-in XML offline.

## Package icon

The editable source, `icon.svg`, and the PNG exports are in the repository root: [SVG](https://github.com/berik-99/Msmq.NetCore/blob/main/icon.svg), [PNG 128](https://raw.githubusercontent.com/berik-99/Msmq.NetCore/main/icon.png), [PNG 256](https://raw.githubusercontent.com/berik-99/Msmq.NetCore/main/icon-256.png), and [PNG 512](https://raw.githubusercontent.com/berik-99/Msmq.NetCore/main/icon-512.png).

Run `pwsh -File tools/generate_icons.ps1` with Chrome or Edge installed to regenerate all three PNGs and copy the 128 pixel icon to `Sources/Assets/icon.png`. That folder contains only the package icon. NuGet includes this icon and the root README; the SVG and preview exports stay in the repository. Icon generation is a maintenance operation, not a build prerequisite.

## Acknowledgments and license

Based on [MSMQ.Messaging](https://github.com/weloytty/MSMQ.Messaging), by Bill Loytty, and the original System.Messaging implementation. Library source code is licensed under MIT; see LICENSE. The bundled Microsoft-derived API documentation is licensed under CC BY 4.0; see ThirdPartyNotices.txt and LICENSE-API-DOCS.txt in the package. NuGet metadata identifies both licenses as `MIT AND CC-BY-4.0`.
