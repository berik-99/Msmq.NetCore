using System;

namespace Msmq.NetCore.Messaging;

public class PeekCompletedEventArgs : EventArgs
{
	private Message message;
	private readonly MessageQueue sender;

	internal PeekCompletedEventArgs(MessageQueue sender, IAsyncResult result)
	{
		AsyncResult = result;
		this.sender = sender;
	}

	public IAsyncResult AsyncResult { get; set; }

	public Message Message => message ?? (message = sender.EndPeek(AsyncResult));
}
