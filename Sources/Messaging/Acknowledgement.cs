using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

public enum Acknowledgment
{
	None = 0,

	ReachQueue = NativeMethods.MESSAGE_CLASS_REACH_QUEUE,

	Receive = NativeMethods.MESSAGE_CLASS_RECEIVE,

	BadDestinationQueue = NativeMethods.MESSAGE_CLASS_BAD_DESTINATION_QUEUE,

	Purged = NativeMethods.MESSAGE_CLASS_PURGED,

	ReachQueueTimeout = NativeMethods.MESSAGE_CLASS_REACH_QUEUE_TIMEOUT,

	QueueExceedMaximumSize = NativeMethods.MESSAGE_CLASS_QUEUE_EXCEED_QUOTA,

	AccessDenied = NativeMethods.MESSAGE_CLASS_ACCESS_DENIED,

	HopCountExceeded = NativeMethods.MESSAGE_CLASS_HOP_COUNT_EXCEEDED,

	BadSignature = NativeMethods.MESSAGE_CLASS_BAD_SIGNATURE,

	BadEncryption = NativeMethods.MESSAGE_CLASS_BAD_ENCRYPTION,

	CouldNotEncrypt = NativeMethods.MESSAGE_CLASS_COULD_NOT_ENCRYPT,

	NotTransactionalQueue = NativeMethods.MESSAGE_CLASS_NOT_TRANSACTIONAL_QUEUE,

	NotTransactionalMessage = NativeMethods.MESSAGE_CLASS_NOT_TRANSACTIONAL_MESSAGE,

	QueueDeleted = NativeMethods.MESSAGE_CLASS_QUEUE_DELETED,

	QueuePurged = NativeMethods.MESSAGE_CLASS_QUEUE_PURGED,

	ReceiveTimeout = NativeMethods.MESSAGE_CLASS_RECEIVE_TIMEOUT,
}
