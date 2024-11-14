using System;

namespace Msmq.NetCore.Messaging;

[Flags]
public enum GenericAccessRights
{
	Read = 1 << 31,
	None = 0,
	All = 1 << 28,
	Execute = 1 << 29,
	Write = 1 << 30
}
