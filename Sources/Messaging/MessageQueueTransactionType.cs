using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public enum MessageQueueTransactionType
{
	None = NativeMethods.QUEUE_TRANSACTION_NONE,
	Automatic = NativeMethods.QUEUE_TRANSACTION_MTS,
	Single = NativeMethods.QUEUE_TRANSACTION_SINGLE,
}
