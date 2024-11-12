
using System;
using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

[Flags]
public enum AcknowledgeTypes
{
	None = NativeMethods.ACKNOWLEDGE_NONE,

	PositiveArrival = NativeMethods.ACKNOWLEDGE_POSITIVE_ARRIVAL,

	PositiveReceive = NativeMethods.ACKNOWLEDGE_POSITIVE_RECEIVE,

	NotAcknowledgeReachQueue = NativeMethods.ACKNOWLEDGE_NEGATIVE_ARRIVAL,

	FullReachQueue = NotAcknowledgeReachQueue | PositiveArrival,

	NegativeReceive = NativeMethods.ACKNOWLEDGE_NEGATIVE_RECEIVE,

	NotAcknowledgeReceive = NegativeReceive | NativeMethods.ACKNOWLEDGE_NEGATIVE_ARRIVAL,

	FullReceive = NotAcknowledgeReceive | PositiveReceive,
}
