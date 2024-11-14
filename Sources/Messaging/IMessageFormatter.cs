using System;
using System.ComponentModel;
using Msmq.NetCore.Messaging.Design;

namespace Msmq.NetCore.Messaging;

[TypeConverter(typeof(MessageFormatterConverter))]
public interface IMessageFormatter : ICloneable
{
	bool CanRead(Message message);

	object Read(Message message);

	void Write(Message message, object obj);
}
