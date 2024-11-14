using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum PeekAction
{
	Current = NativeMethods.QUEUE_ACTION_PEEK_CURRENT,

	Next = NativeMethods.QUEUE_ACTION_PEEK_NEXT
}
