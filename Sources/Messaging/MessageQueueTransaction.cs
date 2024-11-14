
using System;
using System.Threading;
using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public class MessageQueueTransaction : IDisposable
{
	private ITransaction internalTransaction;
	private bool disposed;

	public MessageQueueTransaction() => Status = MessageQueueTransactionStatus.Initialized;

	internal ITransaction InnerTransaction => internalTransaction;

	public MessageQueueTransactionStatus Status { get; private set; }

	public void Abort()
	{
		lock (this)
		{
			if (internalTransaction == null)
			{
				throw new InvalidOperationException(Res.GetString(Res.TransactionNotStarted));
			}
			else
			{
				AbortInternalTransaction();
			}
		}
	}

	private void AbortInternalTransaction()
	{
		int status = internalTransaction.Abort(0, 0, 0);
		if (MessageQueue.IsFatalError(status))
			throw new MessageQueueException(status);

		internalTransaction = null;
		Status = MessageQueueTransactionStatus.Aborted;
	}

	public void Begin()
	{
		//Won't allow begining a new transaction after the object has been disposed.
		if (disposed)
			throw new ObjectDisposedException(GetType().Name);

		lock (this)
		{
			if (internalTransaction != null)
			{
				throw new InvalidOperationException(Res.GetString(Res.TransactionStarted));
			}
			else
			{
				int status = SafeNativeMethods.MQBeginTransaction(out internalTransaction);
				if (MessageQueue.IsFatalError(status))
				{
					internalTransaction = null;
					throw new MessageQueueException(status);
				}

				Status = MessageQueueTransactionStatus.Pending;
			}
		}
	}

	internal ITransaction BeginQueueOperation()
	{
		//@TODO: This overload of Monitor.Enter is obsolete.  Please change this to use Monitor.Enter(ref bool), and remove the pragmas   -- ericeil
		Monitor.Enter(this);
		return internalTransaction;
	}

	public void Commit()
	{
		lock (this)
		{
			if (internalTransaction == null)
			{
				throw new InvalidOperationException(Res.GetString(Res.TransactionNotStarted));
			}
			else
			{
				int status = internalTransaction.Commit(0, 0, 0);
				if (MessageQueue.IsFatalError(status))
					throw new MessageQueueException(status);

				internalTransaction = null;
				Status = MessageQueueTransactionStatus.Committed;
			}
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			lock (this)
			{
				if (internalTransaction != null)
					AbortInternalTransaction();
			}
		}

		disposed = true;
	}

	~MessageQueueTransaction()
	{
		Dispose(false);
	}

	internal void EndQueueOperation()
	{
		Monitor.Exit(this);
	}
}
