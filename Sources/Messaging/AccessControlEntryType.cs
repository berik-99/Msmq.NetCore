using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public enum AccessControlEntryType
{
	Allow = NativeMethods.GRANT_ACCESS,
	Set = NativeMethods.SET_ACCESS,
	Deny = NativeMethods.DENY_ACCESS,
	Revoke = NativeMethods.REVOKE_ACCESS
}
