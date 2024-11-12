using System;
using System.ComponentModel;
using NetCore.Msmq.Messaging.Design;

namespace NetCore.Msmq.Messaging;

[TypeConverter(typeof(MessageFormatterConverter))]
public interface IMessageFormatter : ICloneable
{
	bool CanRead(Message message);

	object Read(Message message);

	void Write(Message message, object obj);
}
