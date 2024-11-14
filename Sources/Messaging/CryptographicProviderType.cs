using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum CryptographicProviderType
{
	None = 0,
	RsaFull = NativeMethods.PROV_RSA_FULL,
	RsqSig = NativeMethods.PROV_RSA_SIG,
	Dss = NativeMethods.PROV_DSS,
	Fortezza = NativeMethods.PROV_FORTEZZA,
	MicrosoftExchange = NativeMethods.PROV_MS_EXCHANGE,
	Ssl = NativeMethods.PROV_SSL,
	SttMer = NativeMethods.PROV_STT_MER,
	SttAcq = NativeMethods.PROV_STT_ACQ,
	SttBrnd = NativeMethods.PROV_STT_BRND,
	SttRoot = NativeMethods.PROV_STT_ROOT,
	SttIss = NativeMethods.PROV_STT_ISS,
}
