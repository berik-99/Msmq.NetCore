using System;

namespace NetCore.Msmq.Messaging;

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
