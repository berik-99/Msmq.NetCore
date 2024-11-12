using System;

namespace NetCore.Msmq.Messaging;

public class ReceiveCompletedEventArgs : EventArgs
{
	private Message message;
	private readonly MessageQueue sender;

	internal ReceiveCompletedEventArgs(MessageQueue sender, IAsyncResult result)
	{
		AsyncResult = result;
		this.sender = sender;
	}

	public IAsyncResult AsyncResult { get; set; }

	public Message Message => message ?? (message = sender.EndReceive(AsyncResult));
}
