using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum EncryptionRequired
{
	None = NativeMethods.QUEUE_PRIVACY_LEVEL_NONE,

	Optional = NativeMethods.QUEUE_PRIVACY_LEVEL_OPTIONAL,

	Body = NativeMethods.QUEUE_PRIVACY_LEVEL_BODY
}
