using System;
using System.ComponentModel;

namespace Msmq.NetCore.Messaging;

[AttributeUsage(AttributeTargets.All)]
public class MessagingDescriptionAttribute : DescriptionAttribute
{
	private bool replaced = false;

	public MessagingDescriptionAttribute(string description)
		: base(description)
	{
	}

	public override string Description
	{
		get
		{
			if (!replaced)
			{
				replaced = true;
				DescriptionValue = Res.GetString(base.Description);
			}
			return base.Description;
		}
	}
}
