# 🌐 NetCore.Msmq

**NetCore.Msmq** is an updated fork of **MSMQ.Messaging**, intended as a drop-in replacement for **System.Messaging** in .NET Core. Thanks to the original project, we've created this version with support for the latest .NET technologies. 🎉

> **Note**: NetCore.Msmq is compatible only with **.NET Standard 2.1** (Windows-only). For applications targeting .NET Framework, it is recommended to use **System.Messaging** instead.

## 🙏 Acknowledgments

A big thanks to **MSMQ.Messaging** for laying the foundation for this project. This fork was developed to address the needs of those looking to upgrade legacy applications, keeping the simplicity of the original project while incorporating support for newer .NET versions.

## 🚀 Getting Started

### Installation

You can install **NetCore.Msmq** directly from [NuGet](https://www.nuget.org/packages/MSMQ.Messaging/), or clone and build the project manually.

### Build Instructions

1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/NetCore.Msmq.git
   ```
2. Navigate to the source folder and build the project or open .sln file in Visual Studio:
   ```bash
   cd NetCore.Msmq/Sources
   dotnet build
   ```

## 📝 Important Notes

1. **NetCore.Msmq** relies on `mqrt.dll`, so it will only work on Windows.
2. Queue configuration and MSMQ installation should work, but test coverage might be limited.
3. The API is compatible with the documentation for [System.Messaging](https://docs.microsoft.com/en-us/dotnet/api/system.messaging?view=netframework-4.8) in the classic .NET Framework.

## 🔄 Upgrading .NET Framework Projects

**NetCore.Msmq** is designed to be easy to use as a replacement for **System.Messaging** in .NET Core projects. To update legacy code, you only need to:

1. Change the reference from `System.Messaging` to `NetCore.Msmq`.
2. Update `using` statements from `System.Messaging` to `NetCore.Msmq`.

All other code should remain the same. 🌟

## 💡 Contributing

We're thrilled that you'd like to contribute! 🎉 This fork preserves the base architecture of **MSMQ.Messaging**, so we encourage anyone looking to extend functionality to create their own fork. If you have improvements that benefit everyone, feel free to open a pull request.

---
