namespace NetCore.Msmq.Messaging;

public enum MessageQueueTransactionStatus
{
	Aborted = 0,
	Committed = 1,
	Initialized = 2,
	Pending = 3,
}
