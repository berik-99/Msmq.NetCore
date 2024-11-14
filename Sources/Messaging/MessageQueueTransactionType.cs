using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum MessageQueueTransactionType
{
	None = NativeMethods.QUEUE_TRANSACTION_NONE,
	Automatic = NativeMethods.QUEUE_TRANSACTION_MTS,
	Single = NativeMethods.QUEUE_TRANSACTION_SINGLE,
}
