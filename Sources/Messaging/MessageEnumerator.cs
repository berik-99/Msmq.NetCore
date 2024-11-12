using System;
using System.Collections;
using System.ComponentModel;
using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public class MessageEnumerator : MarshalByRefObject, IEnumerator, IDisposable
{
	private readonly MessageQueue owner;
	private CursorHandle handle = Interop.CursorHandle.NullHandle;
	private int index = 0;
	private bool disposed = false;
	private readonly bool useCorrectRemoveCurrent = false; //needed in fix for 88615

	internal MessageEnumerator(MessageQueue owner, bool useCorrectRemoveCurrent)
	{
		this.owner = owner;
		this.useCorrectRemoveCurrent = useCorrectRemoveCurrent;
	}

	public Message Current
	{
		get
		{
			if (index == 0)
				throw new InvalidOperationException(Res.GetString(Res.NoCurrentMessage));

			return owner.ReceiveCurrent(TimeSpan.Zero, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, Handle,
														  owner.MessageReadPropertyFilter, null,
														  MessageQueueTransactionType.None);
		}
	}

	object IEnumerator.Current => Current;

	public IntPtr CursorHandle => Handle.DangerousGetHandle();

	internal CursorHandle Handle
	{
		get
		{
			//Cursor handle doesn't demand permissions since GetEnumerator will demand somehow.
			if (handle.IsInvalid)
			{
				//Cannot allocate the a new cursor if the object has been disposed, since finalization has been suppressed.
				if (disposed)
					throw new ObjectDisposedException(GetType().Name);

				int status = SafeNativeMethods.MQCreateCursor(owner.MQInfo.ReadHandle, out var result);
				if (MessageQueue.IsFatalError(status))
					throw new MessageQueueException(status);

				handle = result;
			}
			return handle;
		}
	}

	public void Close()
	{
		index = 0;
		if (!handle.IsInvalid)
		{
			handle.Close();
		}
	}

	public void Dispose()
	{
		Dispose(true);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			Close();
		}

		disposed = true;
	}

	public bool MoveNext()
	{
		return MoveNext(TimeSpan.Zero);
	}

	public unsafe bool MoveNext(TimeSpan timeout)
	{
		long timeoutInMilliseconds = (long)timeout.TotalMilliseconds;
		if (timeoutInMilliseconds < 0 || timeoutInMilliseconds > uint.MaxValue)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "timeout", timeout.ToString()));

		int status = 0;
		int action = NativeMethods.QUEUE_ACTION_PEEK_NEXT;
		//Peek current or next?
		if (index == 0)
			action = NativeMethods.QUEUE_ACTION_PEEK_CURRENT;

		status = owner.StaleSafeReceiveMessage((uint)timeoutInMilliseconds, action, null, null, null, Handle, (IntPtr)NativeMethods.QUEUE_TRANSACTION_NONE);
		//If the cursor reached the end of the queue.
		if (status == (int)MessageQueueErrorCode.IOTimeout)
		{
			Close();
			return false;
		}
		//If all messages were removed.
		else if (status == (int)MessageQueueErrorCode.IllegalCursorAction)
		{
			index = 0;
			Close();
			return false;
		}

		if (MessageQueue.IsFatalError(status))
			throw new MessageQueueException(status);

		++index;
		return true;
	}

	public Message RemoveCurrent()
	{
		return RemoveCurrent(TimeSpan.Zero, null, MessageQueueTransactionType.None);
	}

	public Message RemoveCurrent(MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return RemoveCurrent(TimeSpan.Zero, transaction, MessageQueueTransactionType.None);
	}

	public Message RemoveCurrent(MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return RemoveCurrent(TimeSpan.Zero, null, transactionType);
	}

	public Message RemoveCurrent(TimeSpan timeout)
	{
		return RemoveCurrent(timeout, null, MessageQueueTransactionType.None);
	}

	public Message RemoveCurrent(TimeSpan timeout, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return RemoveCurrent(timeout, transaction, MessageQueueTransactionType.None);
	}

	public Message RemoveCurrent(TimeSpan timeout, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return RemoveCurrent(timeout, null, transactionType);
	}

	private Message RemoveCurrent(TimeSpan timeout, MessageQueueTransaction transaction, MessageQueueTransactionType transactionType)
	{
		long timeoutInMilliseconds = (long)timeout.TotalMilliseconds;
		if (timeoutInMilliseconds < 0 || timeoutInMilliseconds > uint.MaxValue)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "timeout", timeout.ToString()));

		if (index == 0)
			return null;

		Message message = owner.ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE,
																		   Handle, owner.MessageReadPropertyFilter, transaction, transactionType);

		if (!useCorrectRemoveCurrent) --index;

		return message;
	}

	public void Reset()
	{
		Close();
	}
}
