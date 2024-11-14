using System;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization.Formatters;

namespace Msmq.NetCore.Messaging.Design;

/// <include file='..\..\..\doc\MessageFormatterConverter.uex' path='docs/doc[@for="MessageFormatterConverter"]/*' />
/// <internalonly/>
internal class MessageFormatterConverter : ExpandableObjectConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
	{
		return sourceType == typeof(string);
	}

	/// <include file='..\..\..\doc\MessageFormatterConverter.uex' path='docs/doc[@for="MessageFormatterConverter.CanConvertTo"]/*' />
	/// <devdoc>
	///    <para>Gets a value indicating whether this converter can
	///       convert an object to the given destination type using the context.</para>
	/// </devdoc>
	public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
	{
		if (destinationType == typeof(InstanceDescriptor))
		{
			return true;
		}
		return base.CanConvertTo(context, destinationType);
	}

	/// <include file='..\..\..\doc\MessageFormatterConverter.uex' path='docs/doc[@for="MessageFormatterConverter.ConvertFrom"]/*' />
	/// <internalonly/>                                                  
	public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
	{
		if (value != null && value is string s)
		{
			if (s == typeof(ActiveXMessageFormatter).Name)
				return new ActiveXMessageFormatter();
			if (s == typeof(BinaryMessageFormatter).Name)
				return new BinaryMessageFormatter();
			if (s == typeof(XmlMessageFormatter).Name)
				return new XmlMessageFormatter();
		}

		return null;
	}

	/// <include file='..\..\..\doc\MessageFormatterConverter.uex' path='docs/doc[@for="MessageFormatterConverter.ConvertTo"]/*' />
	/// <internalonly/>                 
	public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
	{
		if (destinationType != null && destinationType == typeof(string))
		{
			if (value == null)
				return Res.GetString(Res.toStringNone);

			return value.GetType().Name;
		}
		if (destinationType == typeof(InstanceDescriptor))
		{
			if (value is XmlMessageFormatter f1)
			{
				ConstructorInfo ctor = typeof(XmlMessageFormatter).GetConstructor(new Type[] { typeof(string[]) });
				if (ctor != null)
				{
					return new InstanceDescriptor(ctor, new object[] { f1.TargetTypeNames });
				}
			}
			else if (value is ActiveXMessageFormatter)
			{
				ConstructorInfo ctor = typeof(ActiveXMessageFormatter).GetConstructor(Array.Empty<Type>());
				if (ctor != null)
				{
					return new InstanceDescriptor(ctor, Array.Empty<object>());
				}
			}
			else if (value is BinaryMessageFormatter f)
			{
				ConstructorInfo ctor = typeof(BinaryMessageFormatter).GetConstructor(new Type[] {
					typeof(FormatterAssemblyStyle), typeof(FormatterTypeStyle) });

				if (ctor != null)
				{
					return new InstanceDescriptor(ctor, new object[] { f.TopObjectFormat, f.TypeFormat });
				}
			}
		}

		return base.ConvertTo(context, culture, value, destinationType);
	}

	/// <include file='..\..\..\doc\MessageFormatterConverter.uex' path='docs/doc[@for="MessageFormatterConverter.GetStandardValues"]/*' />
	/// <internalonly/>            
	public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
	{
		StandardValuesCollection values = new StandardValuesCollection(new object[] { new ActiveXMessageFormatter(),
																									   new BinaryMessageFormatter(),
																									   new XmlMessageFormatter(),
																									   null });

		return values;
	}

	public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
	{
		return true;
	}

	/// <include file='..\..\..\doc\MessageFormatterConverter.uex' path='docs/doc[@for="MessageFormatterConverter.GetStandardValuesSupported"]/*' />
	/// <internalonly/>                        
	public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
	{
		return true;
	}
}
