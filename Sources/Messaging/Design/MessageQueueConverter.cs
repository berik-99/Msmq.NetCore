using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace Msmq.NetCore.Messaging.Design;

/// <include file='..\..\..\doc\MessageQueueConverter.uex' path='docs/doc[@for="MessageQueueConverter"]/*' />
/// <internalonly/>
internal class MessageQueueConverter : TypeConverter
{
	private static readonly Hashtable componentsCreated = new Hashtable(StringComparer.InvariantCultureIgnoreCase);

	/// <include file='..\..\..\doc\MessageQueueConverter.uex' path='docs/doc[@for="MessageQueueConverter.CanConvertFrom"]/*' />
	/// <internalonly/>                               
	public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
	{
		if (sourceType == typeof(string))
		{
			return true;
		}
		return base.CanConvertFrom(context, sourceType);
	}

	internal static void AddToCache(MessageQueue queue)
	{
		componentsCreated[queue.Path] = queue;
	}

	/// <include file='..\..\..\doc\MessageQueueConverter.uex' path='docs/doc[@for="MessageQueueConverter.ConvertFrom"]/*' />
	/// <internalonly/>                 
	public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
	{
		if (value != null && value is string)
		{
			string text = ((string)value).Trim();

			if (string.IsNullOrEmpty(text))
				return null;

			if (text.CompareTo(Res.GetString(Res.NotSet)) != 0)
			{
				MessageQueue newQueue = GetFromCache(text);
				if (newQueue != null)
				{
					return newQueue;
				}
				else
				{
					newQueue = new MessageQueue(text);
					AddToCache(newQueue);
					if (context != null)
						context.Container.Add(newQueue);

					return newQueue;
				}
			}
		}

		return null;
	}

	/// <include file='..\..\..\doc\MessageQueueConverter.uex' path='docs/doc[@for="MessageQueueConverter.ConvertTo"]/*' />
	/// <internalonly/>                 
	public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
	{
		if (destinationType != null && destinationType == typeof(string))
		{
			if (value != null)
				return ((MessageQueue)value).Path;
			else
				return Res.GetString(Res.NotSet);
		}

		return base.ConvertTo(context, culture, value, destinationType);
	}

	internal static MessageQueue GetFromCache(string path)
	{
		if (componentsCreated.ContainsKey(path))
		{
			MessageQueue existingComponent = (MessageQueue)componentsCreated[path];
			if (existingComponent.Site == null)
			{
				componentsCreated.Remove(path);
			}
			else
			{
				if (existingComponent.Path == path)
					return existingComponent;
				else
					componentsCreated.Remove(path);
			}
		}

		return null;
	}
}
