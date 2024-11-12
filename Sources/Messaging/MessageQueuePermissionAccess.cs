using System;

namespace NetCore.Msmq.Messaging;

[Flags]
[Serializable]
public enum MessageQueuePermissionAccess
{
	None = 0,
	Browse = 1 << 1,
	Send = 1 << 2 | Browse,
	Peek = 1 << 3 | Browse,
	Receive = 1 << 4 | Peek,
	Administer = 1 << 5 | Send | Receive | Peek,
}
