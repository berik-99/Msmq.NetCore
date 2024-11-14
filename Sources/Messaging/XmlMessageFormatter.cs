using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Msmq.NetCore.Messaging;

public class XmlMessageFormatter : IMessageFormatter
{
	private Type[] targetTypes;
	private string[] targetTypeNames;
	readonly Hashtable targetSerializerTable = new Hashtable();
	private bool typeNamesAdded;
	private bool typesAdded;

	public XmlMessageFormatter()
	{
		TargetTypes = Array.Empty<Type>();
		TargetTypeNames = Array.Empty<string>();
	}

	public XmlMessageFormatter(string[] targetTypeNames)
	{
		TargetTypeNames = targetTypeNames;
		TargetTypes = Array.Empty<Type>();
	}

	public XmlMessageFormatter(Type[] targetTypes)
	{
		TargetTypes = targetTypes;
		TargetTypeNames = Array.Empty<string>();
	}

	[MessagingDescription(Res.XmlMsgTargetTypeNames)]
	public string[] TargetTypeNames
	{
		get => targetTypeNames;

		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			typeNamesAdded = false;
			targetTypeNames = value;
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.XmlMsgTargetTypes)]
	public Type[] TargetTypes
	{
		get => targetTypes;

		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			typesAdded = false;
			targetTypes = value;
		}
	}

	public bool CanRead(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		CreateTargetSerializerTable();

		Stream stream = message.BodyStream;
		XmlTextReader reader = new XmlTextReader(stream);
		reader.WhitespaceHandling = WhitespaceHandling.Significant;
		reader.DtdProcessing = DtdProcessing.Prohibit;
		bool result = false;
		foreach (XmlSerializer serializer in targetSerializerTable.Values)
		{
			if (serializer.CanDeserialize(reader))
			{
				result = true;
				break;
			}
		}

		message.BodyStream.Position = 0; // reset stream in case CanRead is followed by Deserialize
		return result;
	}

	public object Clone()
	{
		XmlMessageFormatter formatter = new XmlMessageFormatter();
		formatter.targetTypes = targetTypes;
		formatter.targetTypeNames = targetTypeNames;
		formatter.typesAdded = typesAdded;
		formatter.typeNamesAdded = typeNamesAdded;
		foreach (Type targetType in targetSerializerTable.Keys)
			formatter.targetSerializerTable[targetType] = new XmlSerializer(targetType);

		return formatter;
	}

	private void CreateTargetSerializerTable()
	{
		if (!typeNamesAdded)
		{
			for (int index = 0; index < targetTypeNames.Length; ++index)
			{
				Type targetType = Type.GetType(targetTypeNames[index], true);
				if (targetType != null)
					targetSerializerTable[targetType] = new XmlSerializer(targetType);
			}

			typeNamesAdded = true;
		}

		if (!typesAdded)
		{
			for (int index = 0; index < targetTypes.Length; ++index)
				targetSerializerTable[targetTypes[index]] = new XmlSerializer(targetTypes[index]);

			typesAdded = true;
		}

		if (targetSerializerTable.Count == 0)
			throw new InvalidOperationException(Res.GetString(Res.TypeListMissing));
	}

	public object Read(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		CreateTargetSerializerTable();

		Stream stream = message.BodyStream;
		XmlTextReader reader = new XmlTextReader(stream);
		reader.WhitespaceHandling = WhitespaceHandling.Significant;
		reader.DtdProcessing = DtdProcessing.Prohibit;
		foreach (XmlSerializer serializer in targetSerializerTable.Values)
		{
			if (serializer.CanDeserialize(reader))
				return serializer.Deserialize(reader);
		}

		throw new InvalidOperationException(Res.GetString(Res.InvalidTypeDeserialization));
	}

	public void Write(Message message, object obj)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		if (obj == null)
			throw new ArgumentNullException(nameof(obj));

		Stream stream = new MemoryStream();
		Type serializedType = obj.GetType();
		XmlSerializer serializer = null;
		if (targetSerializerTable.ContainsKey(serializedType))
		{
			serializer = (XmlSerializer)targetSerializerTable[serializedType];
		}
		else
		{
			serializer = new XmlSerializer(serializedType);
			targetSerializerTable[serializedType] = serializer;
		}

		serializer.Serialize(stream, obj);
		message.BodyStream = stream;
		//Need to reset the body type, in case the same message
		//is reused by some other formatter.
		message.BodyType = 0;
	}
}
