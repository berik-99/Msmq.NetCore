
using System.Collections.Generic;
using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum QueueAccessMode
{
	Receive = NativeMethods.QUEUE_ACCESS_RECEIVE,
	Send = NativeMethods.QUEUE_ACCESS_SEND,

	SendAndReceive = NativeMethods.QUEUE_ACCESS_SEND | NativeMethods.QUEUE_ACCESS_RECEIVE,
	Peek = NativeMethods.QUEUE_ACCESS_PEEK,
	ReceiveAndAdmin = NativeMethods.QUEUE_ACCESS_RECEIVE | NativeMethods.QUEUE_ACCESS_ADMIN,
	PeekAndAdmin = NativeMethods.QUEUE_ACCESS_PEEK | NativeMethods.QUEUE_ACCESS_ADMIN,
}

internal class QueueAccessModeHolder
{
	private readonly QueueAccessMode accessMode;

	private static readonly Dictionary<QueueAccessMode, QueueAccessModeHolder> holders = new Dictionary<QueueAccessMode, QueueAccessModeHolder>();

	private QueueAccessModeHolder(QueueAccessMode accessMode) => this.accessMode = accessMode;

	public static QueueAccessModeHolder GetQueueAccessModeHolder(QueueAccessMode accessMode)
	{
		if (holders.TryGetValue(accessMode, out QueueAccessModeHolder value))
		{
			return value;
		}

		lock (holders)
		{
			QueueAccessModeHolder newHolder = new QueueAccessModeHolder(accessMode);
			holders[accessMode] = newHolder;
			return newHolder;
		}
	}

	public bool CanRead()
	{
		return (accessMode & QueueAccessMode.Receive) != 0 || (accessMode & QueueAccessMode.Peek) != 0;
	}

	public bool CanWrite()
	{
		return (accessMode & QueueAccessMode.Send) != 0;
	}

	public int GetReadAccessMode()
	{
		int result = (int)(accessMode & ~QueueAccessMode.Send);
		if (result != 0)
			return result;
		// this is fail-fast path, when we know right away that the operation is incompatible with access mode
		// AccessDenied can also happen in other cases,
		// (for example, when we try to receive on a queue opened only for peek.
		// We'll let MQReceiveMessage enforce these rules
		throw new MessageQueueException((int)MessageQueueErrorCode.AccessDenied);
	}

	public int GetWriteAccessMode()
	{
		int result = (int)(accessMode & QueueAccessMode.Send);
		if (result != 0)
			return result;
		// this is fail-fast path, when we know right away that the operation is incompatible with access mode
		// AccessDenied can also happen in other cases,
		// (for example, when we try to receive on a queue opened only for peek.
		// We'll let MQReceiveMessage enforce these rules
		throw new MessageQueueException((int)MessageQueueErrorCode.AccessDenied);
	}
}
