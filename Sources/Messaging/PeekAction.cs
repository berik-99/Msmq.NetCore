using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public enum PeekAction
{
	Current = NativeMethods.QUEUE_ACTION_PEEK_CURRENT,

	Next = NativeMethods.QUEUE_ACTION_PEEK_NEXT
}
