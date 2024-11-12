using System;
using System.ComponentModel;

namespace NetCore.Msmq.Messaging;

public class AccessControlEntry
{
	//const int customRightsMask   = 0x0000ffff;
	const StandardAccessRights standardRightsMask = (StandardAccessRights)0x001f0000;
	const GenericAccessRights genericRightsMask = unchecked((GenericAccessRights)0xf0000000);

	internal int accessFlags = 0;
	Trustee trustee = null;
	AccessControlEntryType entryType = AccessControlEntryType.Allow;

	public AccessControlEntry()
	{
	}

	public AccessControlEntry(Trustee trustee) => Trustee = trustee;

	public AccessControlEntry(Trustee trustee, GenericAccessRights genericAccessRights, StandardAccessRights standardAccessRights, AccessControlEntryType entryType)
	{
		GenericAccessRights = genericAccessRights;
		StandardAccessRights = standardAccessRights;
		Trustee = trustee;
		EntryType = entryType;
	}

	public AccessControlEntryType EntryType
	{
		get => entryType;
		set
		{
			if (!ValidationUtility.ValidateAccessControlEntryType(value))
				throw new InvalidEnumArgumentException("value", (int)value, typeof(AccessControlEntryType));

			entryType = value;
		}
	}

	protected int CustomAccessRights
	{
		get => accessFlags;
		set => accessFlags = value;
	}

	public GenericAccessRights GenericAccessRights
	{
		get => (GenericAccessRights)accessFlags & genericRightsMask;
		set
		{
			// make sure these flags really are genericAccessRights
			if ((value & genericRightsMask) != value)
				throw new InvalidEnumArgumentException("value", (int)value, typeof(GenericAccessRights));

			accessFlags = accessFlags & (int)~genericRightsMask | (int)value;
		}
	}

	public StandardAccessRights StandardAccessRights
	{
		get => (StandardAccessRights)accessFlags & standardRightsMask;
		set
		{
			// make sure these flags really are standardAccessRights
			if ((value & standardRightsMask) != value)
				throw new InvalidEnumArgumentException("value", (int)value, typeof(StandardAccessRights));

			accessFlags = accessFlags & (int)~standardRightsMask | (int)value;
		}
	}

	public Trustee Trustee
	{
		get => trustee;
		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			trustee = value;
		}
	}
}
