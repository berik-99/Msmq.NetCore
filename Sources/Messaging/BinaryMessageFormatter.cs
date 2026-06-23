using System.ComponentModel;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace Msmq.NetCore.Messaging;

/// <summary>
/// BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing.
/// See https://aka.ms/binaryformatter for more information. Use XmlMessageFormatter instead.
/// This API is only supported on Windows.
/// </summary>
[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information. Use XmlMessageFormatter instead.")]
public class BinaryMessageFormatter : IMessageFormatter
{
	private readonly BinaryFormatter formatter;
	internal const short VT_BINARY_OBJECT = 0x300;

	/// <summary>
	/// Initializes a new instance of the BinaryMessageFormatter class.
	/// This API is only supported on Windows and is obsolete due to BinaryFormatter security risks.
	/// </summary>
	[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information.")]
	public BinaryMessageFormatter() => formatter = new BinaryFormatter();

	/// <summary>
	/// Initializes a new instance of the BinaryMessageFormatter class with specified formatting options.
	/// This API is only supported on Windows and is obsolete due to BinaryFormatter security risks.
	/// </summary>
	[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information.")]
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

	/// <summary>
	/// Determines whether the formatter can deserialize the message.
	/// This API is only supported on Windows and is obsolete due to BinaryFormatter security risks.
	/// </summary>
	[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information.")]
	public bool CanRead(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		int variantType = message.BodyType;
		return variantType == VT_BINARY_OBJECT;
	}

	/// <summary>
	/// Creates a clone of the formatter.
	/// This API is only supported on Windows and is obsolete due to BinaryFormatter security risks.
	/// </summary>
	[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information.")]
	public object Clone()
	{
		return new BinaryMessageFormatter(TopObjectFormat, TypeFormat);
	}

	/// <summary>
	/// Deserializes a message body from the specified Message object.
	/// This API is only supported on Windows and is obsolete due to BinaryFormatter security risks.
	/// </summary>
	[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information.")]
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

	/// <summary>
	/// Serializes an object to the body of the specified Message.
	/// This API is only supported on Windows and is obsolete due to BinaryFormatter security risks.
	/// </summary>
	[Obsolete("BinaryMessageFormatter is obsolete because BinaryFormatter is dangerous and is not recommended for data processing. See https://aka.ms/binaryformatter for more information.")]
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
