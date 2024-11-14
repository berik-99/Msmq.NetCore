using System;
using System.ComponentModel;

namespace Msmq.NetCore.Messaging;

public class Trustee
{
	string name;
	TrusteeType trusteeType;

	public string Name
	{
		get => name;
		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			name = value;
		}
	}

	public string SystemName { get; set; }

	public TrusteeType TrusteeType
	{
		get => trusteeType;
		set
		{
			if (!ValidationUtility.ValidateTrusteeType(value))
				throw new InvalidEnumArgumentException("value", (int)value, typeof(TrusteeType));

			trusteeType = value;
		}
	}

	public Trustee()
	{
	}

	public Trustee(string name) : this(name, null) { }

	public Trustee(string name, string systemName) : this(name, systemName, TrusteeType.Unknown) { }

	public Trustee(string name, string systemName, TrusteeType trusteeType)
	{
		Name = name;
		SystemName = systemName;
		TrusteeType = trusteeType;
	}
}
