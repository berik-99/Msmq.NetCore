using System;
using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public sealed class Cursor : IDisposable
{
	private CursorHandle handle;
	private bool disposed;

	internal Cursor(MessageQueue queue)
	{
		int status = SafeNativeMethods.MQCreateCursor(queue.MQInfo.ReadHandle, out var result);
		if (MessageQueue.IsFatalError(status))
			throw new MessageQueueException(status);

		handle = result;
	}

	internal CursorHandle Handle
	{
		get
		{
			if (disposed)
			{
				throw new ObjectDisposedException(GetType().Name);
			}

			return handle;
		}
	}

	public void Close()
	{
		if (handle != null)
		{
			handle.Close();
			handle = null;
		}
	}

	public void Dispose()
	{
		Close();
		disposed = true;
	}
}
