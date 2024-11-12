using System;

namespace NetCore.Msmq.Messaging;

[Flags]
public enum MessageQueueAccessRights
{
	DeleteMessage = 0x00000001,
	PeekMessage = 0x00000002,

	ReceiveMessage = DeleteMessage | PeekMessage,
	WriteMessage = 0x00000004,
	DeleteJournalMessage = 0x00000008,

	ReceiveJournalMessage = DeleteJournalMessage | PeekMessage,
	SetQueueProperties = 0x00000010,
	GetQueueProperties = 0x00000020,
	DeleteQueue = 0x00010000,
	GetQueuePermissions = 0x00020000,

	GenericWrite = GetQueueProperties | GetQueuePermissions | WriteMessage,

	GenericRead = GetQueueProperties | GetQueuePermissions | ReceiveMessage | ReceiveJournalMessage,
	ChangeQueuePermissions = 0x00040000,
	TakeQueueOwnership = 0x00080000,

	FullControl = DeleteMessage | PeekMessage | WriteMessage | DeleteJournalMessage |
							 SetQueueProperties | GetQueueProperties | DeleteQueue | GetQueuePermissions |
							 ChangeQueuePermissions | TakeQueueOwnership,
}
