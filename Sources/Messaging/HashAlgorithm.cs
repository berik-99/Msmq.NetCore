using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum HashAlgorithm
{
	None = 0,

	Md2 = NativeMethods.CALG_MD2,

	Md4 = NativeMethods.CALG_MD4,

	Md5 = NativeMethods.CALG_MD5,

	Sha = NativeMethods.CALG_SHA,

	Mac = NativeMethods.CALG_MAC,

	Sha256 = NativeMethods.CALG_SHA256,

	Sha384 = NativeMethods.CALG_SHA384,

	Sha512 = NativeMethods.CALG_SHA512,
}
