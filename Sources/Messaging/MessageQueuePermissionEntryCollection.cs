using System;
using System.Collections;

namespace NetCore.Msmq.Messaging;

[Serializable]
public class MessageQueuePermissionEntryCollection : CollectionBase
{
	readonly MessageQueuePermission owner;

	internal MessageQueuePermissionEntryCollection(MessageQueuePermission owner) => this.owner = owner;

	public MessageQueuePermissionEntry this[int index]
	{
		get => (MessageQueuePermissionEntry)List[index];
		set => List[index] = value;
	}

	public int Add(MessageQueuePermissionEntry value)
	{
		return List.Add(value);
	}

	public void AddRange(MessageQueuePermissionEntry[] value)
	{
		if (value == null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		for (int i = 0; i < value.Length; i = i + 1)
		{
			Add(value[i]);
		}
	}

	public void AddRange(MessageQueuePermissionEntryCollection value)
	{
		if (value == null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		int currentCount = value.Count;
		for (int i = 0; i < currentCount; i = i + 1)
		{
			Add(value[i]);
		}
	}

	public bool Contains(MessageQueuePermissionEntry value)
	{
		return List.Contains(value);
	}

	public void CopyTo(MessageQueuePermissionEntry[] array, int index)
	{
		List.CopyTo(array, index);
	}

	public int IndexOf(MessageQueuePermissionEntry value)
	{
		return List.IndexOf(value);
	}

	public void Insert(int index, MessageQueuePermissionEntry value)
	{
		List.Insert(index, value);
	}

	public void Remove(MessageQueuePermissionEntry value)
	{
		List.Remove(value);
	}

	protected override void OnClear()
	{
		owner.Clear();
	}

	protected override void OnInsert(int index, object value)
	{
		owner.Clear();
	}

	protected override void OnRemove(int index, object value)
	{
		owner.Clear();
	}

	protected override void OnSet(int index, object oldValue, object newValue)
	{
		owner.Clear();
	}
}
