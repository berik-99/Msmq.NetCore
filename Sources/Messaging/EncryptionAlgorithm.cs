using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum EncryptionAlgorithm
{
	None = 0,

	Rc2 = NativeMethods.CALG_RC2,

	Rc4 = NativeMethods.CALG_RC4,
}
