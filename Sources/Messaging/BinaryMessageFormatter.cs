using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace Msmq.NetCore.Messaging;

public class BinaryMessageFormatter : IMessageFormatter
{
	private readonly BinaryFormatter formatter;
	internal const short VT_BINARY_OBJECT = 0x300;

	public BinaryMessageFormatter() => formatter = new BinaryFormatter();

	public BinaryMessageFormatter(FormatterAssemblyStyle topObjectFormat, FormatterTypeStyle typeFormat) => formatter = new BinaryFormatter { AssemblyFormat = topObjectFormat, TypeFormat = typeFormat };

	[MessagingDescription(Res.MsgTopObjectFormat), DefaultValue(FormatterAssemblyStyle.Simple)]
	public FormatterAssemblyStyle TopObjectFormat
	{
		get => formatter.AssemblyFormat;

		set => formatter.AssemblyFormat = value;
	}

	[MessagingDescription(Res.MsgTypeFormat), DefaultValue(FormatterTypeStyle.TypesWhenNeeded)]
	public FormatterTypeStyle TypeFormat
	{
		get => formatter.TypeFormat;

		set => formatter.TypeFormat = value;
	}

	public bool CanRead(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		int variantType = message.BodyType;
		return variantType == VT_BINARY_OBJECT;
	}

	public object Clone()
	{
		return new BinaryMessageFormatter(TopObjectFormat, TypeFormat);
	}

	public object Read(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		int variantType = message.BodyType;
		if (variantType == VT_BINARY_OBJECT)
		{
			Stream stream = message.BodyStream;
			return formatter.Deserialize(stream);
		}

		throw new InvalidOperationException(Res.GetString(Res.InvalidTypeDeserialization));
	}

	public void Write(Message message, object obj)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		Stream stream = new MemoryStream();
		formatter.Serialize(stream, obj);
		message.BodyType = VT_BINARY_OBJECT;
		message.BodyStream = stream;
	}
}
