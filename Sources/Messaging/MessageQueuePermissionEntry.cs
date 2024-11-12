using System;
using System.ComponentModel;

namespace NetCore.Msmq.Messaging;

[Serializable]
public class MessageQueuePermissionEntry
{
	private readonly string label;
	private readonly string machineName;
	private readonly string path;
	private readonly string category;
	private readonly MessageQueuePermissionAccess permissionAccess;

	public MessageQueuePermissionEntry(MessageQueuePermissionAccess permissionAccess, string path)
	{
		if (path == null)
			throw new ArgumentNullException(nameof(path));

		if (path != MessageQueuePermission.Any && !MessageQueue.ValidatePath(path, false))
			throw new ArgumentException(Res.GetString(Res.PathSyntax));

		this.path = path;

		this.permissionAccess = permissionAccess;
	}

	public MessageQueuePermissionEntry(MessageQueuePermissionAccess permissionAccess, string machineName, string label, string category)
	{
		if (machineName == null && label == null && category == null)
			throw new ArgumentNullException(nameof(machineName));

		if (machineName != null && !SyntaxCheck.CheckMachineName(machineName))
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "MachineName", machineName));

		this.permissionAccess = permissionAccess;
		this.machineName = machineName;
		this.label = label;
		this.category = category;
	}

	public string Category => category;

	public string Label => label;

	public string MachineName => machineName;

	public string Path => path;

	public MessageQueuePermissionAccess PermissionAccess => permissionAccess;
}
