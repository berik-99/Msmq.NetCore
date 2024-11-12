using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public enum EncryptionRequired
{
	None = NativeMethods.QUEUE_PRIVACY_LEVEL_NONE,

	Optional = NativeMethods.QUEUE_PRIVACY_LEVEL_OPTIONAL,

	Body = NativeMethods.QUEUE_PRIVACY_LEVEL_BODY
}
