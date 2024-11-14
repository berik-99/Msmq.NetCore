using System;

namespace Msmq.NetCore.Messaging;

[Flags]
public enum StandardAccessRights
{
	None = 0,
	Delete = 1 << 16,
	ReadSecurity = 1 << 17,
	Read = ReadSecurity,
	Write = ReadSecurity,
	Execute = ReadSecurity,
	WriteSecurity = 1 << 18,
	ModifyOwner = 1 << 19,
	Required = Delete | WriteSecurity | ModifyOwner,
	Synchronize = 1 << 20,
	All = Delete | WriteSecurity | ModifyOwner | ReadSecurity | Synchronize
}
