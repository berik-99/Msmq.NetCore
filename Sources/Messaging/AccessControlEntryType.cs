using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum AccessControlEntryType
{
	Allow = NativeMethods.GRANT_ACCESS,
	Set = NativeMethods.SET_ACCESS,
	Deny = NativeMethods.DENY_ACCESS,
	Revoke = NativeMethods.REVOKE_ACCESS
}
