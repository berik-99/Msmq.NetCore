using System;
using System.ComponentModel;
using System.Security;
using System.Security.Permissions;

namespace Msmq.NetCore.Messaging;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Event, AllowMultiple = true, Inherited = false),
Serializable]
public class MessageQueuePermissionAttribute : CodeAccessSecurityAttribute
{
	private string label;
	private string machineName;
	private string path;
	private string category;
	private MessageQueuePermissionAccess permissionAccess;

	public MessageQueuePermissionAttribute(SecurityAction action)
		: base(action)
	{
	}

	public string Category
	{
		get => category;

		set
		{
			string oldValue = category;
			category = value;
			Exception e = CheckProperties();
			if (e != null)
			{
				category = oldValue;
				throw e;
			}
		}
	}

	public string Label
	{
		get => label;

		set
		{
			string oldValue = label;
			label = value;
			Exception e = CheckProperties();
			if (e != null)
			{
				label = oldValue;
				throw e;
			}
		}
	}

	public string MachineName
	{
		get => machineName;

		set
		{
			if (value != null && !SyntaxCheck.CheckMachineName(value))
				throw new ArgumentException(Res.GetString(Res.InvalidProperty, "MachineName", value));

			string oldValue = machineName;
			machineName = value;
			Exception e = CheckProperties();
			if (e != null)
			{
				machineName = oldValue;
				throw e;
			}
		}
	}

	public string Path
	{
		get => path;

		set
		{
			if (value != null && value != MessageQueuePermission.Any && !MessageQueue.ValidatePath(value, false))
				throw new ArgumentException(Res.GetString(Res.PathSyntax));

			string oldValue = path;
			path = value;
			Exception e = CheckProperties();
			if (e != null)
			{
				path = oldValue;
				throw e;
			}
		}
	}

	public MessageQueuePermissionAccess PermissionAccess
	{
		get => permissionAccess;

		set => permissionAccess = value;
	}

	public override IPermission CreatePermission()
	{
		if (Unrestricted)
			return new MessageQueuePermission(PermissionState.Unrestricted);

		CheckProperties();
		if (path != null)
			return new MessageQueuePermission(PermissionAccess, path);

		return new MessageQueuePermission(PermissionAccess, machineName, label, category);
	}

	private Exception CheckProperties()
	{
		if (path != null &&
			(machineName != null || label != null || category != null))
		{
			return new InvalidOperationException(Res.GetString(Res.PermissionPathOrCriteria));
		}

		if (path == null &&
			machineName == null && label == null && category == null)
		{
			return new InvalidOperationException(Res.GetString(Res.PermissionAllNull));
		}

		return null;
	}
}
