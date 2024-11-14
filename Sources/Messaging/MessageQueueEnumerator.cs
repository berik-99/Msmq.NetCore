
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public class MessageQueueEnumerator : MarshalByRefObject, IEnumerator, IDisposable
{
	private readonly MessageQueueCriteria criteria;
	private LocatorHandle locatorHandle = Interop.LocatorHandle.InvalidHandle;
	private MessageQueue currentMessageQueue;
	private readonly bool checkSecurity;
	private bool disposed;

	internal MessageQueueEnumerator(MessageQueueCriteria criteria)
	{
		this.criteria = criteria;
		checkSecurity = true;
	}

	internal MessageQueueEnumerator(MessageQueueCriteria criteria, bool checkSecurity)
	{
		this.criteria = criteria;
		this.checkSecurity = checkSecurity;
	}

	public MessageQueue Current
	{
		get
		{
			if (currentMessageQueue == null)
				throw new InvalidOperationException(Res.GetString(Res.NoCurrentMessageQueue));

			return currentMessageQueue;
		}
	}

	object IEnumerator.Current => Current;

	public void Close()
	{
		if (!locatorHandle.IsInvalid)
		{
			locatorHandle.Close();
			currentMessageQueue = null;
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

	public IntPtr LocatorHandle => Handle.DangerousGetHandle();

	LocatorHandle Handle
	{
		get
		{
			if (locatorHandle.IsInvalid)
			{
				//Cannot allocate the locatorHandle if the object has been disposed, since finalization has been suppressed.
				if (disposed)
					throw new ObjectDisposedException(GetType().Name);

				if (checkSecurity)
				{
					MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, MessageQueuePermission.Any);
					permission.Demand();
				}

				Columns columns = new Columns(2);
				LocatorHandle enumHandle;
				columns.AddColumnId(NativeMethods.QUEUE_PROPID_PATHNAME);
				//Adding the instance property avoids accessing the DS a second
				//time, the formatName can be resolved by calling MQInstanceToFormatName
				columns.AddColumnId(NativeMethods.QUEUE_PROPID_INSTANCE);
				int status;
				if (criteria != null)
					status = UnsafeNativeMethods.MQLocateBegin(null, criteria.Reference, columns.GetColumnsRef(), out enumHandle);
				else
					status = UnsafeNativeMethods.MQLocateBegin(null, null, columns.GetColumnsRef(), out enumHandle);

				if (MessageQueue.IsFatalError(status))
					throw new MessageQueueException(status);

				locatorHandle = enumHandle;
			}

			return locatorHandle;
		}
	}

	public bool MoveNext()
	{
		MQPROPVARIANTS[] array = new MQPROPVARIANTS[2];
		int propertyCount;
		string currentItem;
		byte[] currentGuid = new byte[16];
		string machineName = null;

		if (criteria?.FilterMachine == true)
		{
			if (criteria.MachineName.CompareTo(".") == 0)
				machineName = MessageQueue.ComputerName + "\\";
			else
				machineName = criteria.MachineName + "\\";
		}

		do
		{
			propertyCount = 2;
			int status;
			status = SafeNativeMethods.MQLocateNext(Handle, ref propertyCount, array);
			if (MessageQueue.IsFatalError(status))
				throw new MessageQueueException(status);

			if (propertyCount != 2)
			{
				currentMessageQueue = null;
				return false;
			}

			//Using Unicode API even on Win9x
			currentItem = Marshal.PtrToStringUni(array[0].ptr);
			Marshal.Copy(array[1].ptr, currentGuid, 0, 16);
			//MSMQ allocated this memory, lets free it.
			SafeNativeMethods.MQFreeMemory(array[0].ptr);
			SafeNativeMethods.MQFreeMemory(array[1].ptr);
		}
		while (machineName != null && (machineName.Length >= currentItem.Length ||
									   string.Compare(machineName, 0, currentItem, 0, machineName.Length, true, CultureInfo.InvariantCulture) != 0));

		currentMessageQueue = new MessageQueue(currentItem, new Guid(currentGuid));
		return true;
	}

	public void Reset()
	{
		Close();
	}
}
