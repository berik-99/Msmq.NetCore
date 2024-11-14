namespace Msmq.NetCore.Messaging;

public class MessageQueueAccessControlEntry : AccessControlEntry
{
	public MessageQueueAccessControlEntry(Trustee trustee, MessageQueueAccessRights rights)
		: base(trustee) => CustomAccessRights |= (int)rights;

	public MessageQueueAccessControlEntry(Trustee trustee, MessageQueueAccessRights rights, AccessControlEntryType entryType)
		: base(trustee)
	{
		CustomAccessRights |= (int)rights;
		EntryType = entryType;
	}

	public MessageQueueAccessRights MessageQueueAccessRights
	{
		get => (MessageQueueAccessRights)CustomAccessRights;
		set => CustomAccessRights = (int)value;
	}
}
