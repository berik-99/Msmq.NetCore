using System;
using System.ComponentModel;
using System.Security.Permissions;

namespace NetCore.Msmq.Messaging;

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
		[HostProtection(SharedState = true)] // DescriptionAttribute uses SharedState=true. We should not change base's behavior
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
