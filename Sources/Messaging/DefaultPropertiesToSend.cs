using System;
using System.ComponentModel;
using NetCore.Msmq;
using NetCore.Msmq.Messaging.Design;

namespace NetCore.Msmq.Messaging;

[TypeConverter(typeof(ExpandableObjectConverter))]
public class DefaultPropertiesToSend
{
	private readonly bool designMode;
	private MessageQueue cachedAdminQueue;
	private MessageQueue cachedResponseQueue;
	private MessageQueue cachedTransactionStatusQueue;

	public DefaultPropertiesToSend()
	{
	}

	internal DefaultPropertiesToSend(bool designMode) => this.designMode = designMode;

	[DefaultValue(AcknowledgeTypes.None), MessagingDescription(Res.MsgAcknowledgeType)]
	public AcknowledgeTypes AcknowledgeType
	{
		get => CachedMessage.AcknowledgeType;

		set => CachedMessage.AcknowledgeType = value;
	}

	[DefaultValue(null), MessagingDescription(Res.MsgAdministrationQueue)]
	public MessageQueue AdministrationQueue
	{
		get
		{
			if (designMode)
			{
				if (cachedAdminQueue != null && cachedAdminQueue.Site == null)
					cachedAdminQueue = null;

				return cachedAdminQueue;
			}

			return CachedMessage.AdministrationQueue;
		}

		set
		{
			//The format name of this queue shouldn't be
			//resolved at desgin time, but it should at runtime.
			if (designMode)
				cachedAdminQueue = value;
			else
				CachedMessage.AdministrationQueue = value;
		}
	}

	[DefaultValue(0), MessagingDescription(Res.MsgAppSpecific)]
	public int AppSpecific
	{
		get => CachedMessage.AppSpecific;

		set => CachedMessage.AppSpecific = value;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgAttachSenderId)]
	public bool AttachSenderId
	{
		get => CachedMessage.AttachSenderId;

		set => CachedMessage.AttachSenderId = value;
	}

	internal Message CachedMessage { get; } = new Message();

	[DefaultValue(EncryptionAlgorithm.Rc2), MessagingDescription(Res.MsgEncryptionAlgorithm)]
	public EncryptionAlgorithm EncryptionAlgorithm
	{
		get => CachedMessage.EncryptionAlgorithm;

		set => CachedMessage.EncryptionAlgorithm = value;
	}

	[Editor("System.ComponentModel.Design.ArrayEditor, " + AssemblyRef.SystemDesign, "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
	MessagingDescription(Res.MsgExtension)]
	public byte[] Extension
	{
		get => CachedMessage.Extension;

		set => CachedMessage.Extension = value;
	}

	[DefaultValue(HashAlgorithm.Md5), MessagingDescription(Res.MsgHashAlgorithm)]
	public HashAlgorithm HashAlgorithm
	{
		get => CachedMessage.HashAlgorithm;

		set => CachedMessage.HashAlgorithm = value;
	}

	[DefaultValue(""), MessagingDescription(Res.MsgLabel)]
	public string Label
	{
		get => CachedMessage.Label;

		set => CachedMessage.Label = value;
	}

	[DefaultValue(MessagePriority.Normal), MessagingDescription(Res.MsgPriority)]
	public MessagePriority Priority
	{
		get => CachedMessage.Priority;

		set => CachedMessage.Priority = value;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgRecoverable)]
	public bool Recoverable
	{
		get => CachedMessage.Recoverable;

		set => CachedMessage.Recoverable = value;
	}

	[DefaultValue(null), MessagingDescription(Res.MsgResponseQueue)]
	public MessageQueue ResponseQueue
	{
		get
		{
			if (designMode)
				return cachedResponseQueue;

			return CachedMessage.ResponseQueue;
		}

		set
		{
			//The format name of this queue shouldn't be
			//resolved at desgin time, but it should at runtime.
			if (designMode)
				cachedResponseQueue = value;
			else
				CachedMessage.ResponseQueue = value;
		}
	}

	[TypeConverter(typeof(TimeoutConverter)),
	MessagingDescription(Res.MsgTimeToBeReceived)]
	public TimeSpan TimeToBeReceived
	{
		get => CachedMessage.TimeToBeReceived;

		set => CachedMessage.TimeToBeReceived = value;
	}

	[TypeConverter(typeof(TimeoutConverter)),
	MessagingDescription(Res.MsgTimeToReachQueue)]
	public TimeSpan TimeToReachQueue
	{
		get => CachedMessage.TimeToReachQueue;

		set => CachedMessage.TimeToReachQueue = value;
	}

	[DefaultValue(null), MessagingDescription(Res.MsgTransactionStatusQueue)]
	public MessageQueue TransactionStatusQueue
	{
		get
		{
			if (designMode)
				return cachedTransactionStatusQueue;

			return CachedMessage.TransactionStatusQueue;
		}

		set
		{
			//The format name of this queue shouldn't be
			//resolved at desgin time, but it should at runtime.
			if (designMode)
				cachedTransactionStatusQueue = value;
			else
				CachedMessage.TransactionStatusQueue = value;
		}
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseAuthentication)]
	public bool UseAuthentication
	{
		get => CachedMessage.UseAuthentication;

		set => CachedMessage.UseAuthentication = value;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseDeadLetterQueue)]
	public bool UseDeadLetterQueue
	{
		get => CachedMessage.UseDeadLetterQueue;

		set => CachedMessage.UseDeadLetterQueue = value;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseEncryption)]
	public bool UseEncryption
	{
		get => CachedMessage.UseEncryption;

		set => CachedMessage.UseEncryption = value;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseJournalQueue)]
	public bool UseJournalQueue
	{
		get => CachedMessage.UseJournalQueue;

		set => CachedMessage.UseJournalQueue = value;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseTracing)]
	public bool UseTracing
	{
		get => CachedMessage.UseTracing;

		set => CachedMessage.UseTracing = value;
	}

	private bool ShouldSerializeTimeToBeReceived()
	{
		return TimeToBeReceived != Message.InfiniteTimeout;
	}

	private bool ShouldSerializeTimeToReachQueue()
	{
		return TimeToReachQueue != Message.InfiniteTimeout;
	}

	private bool ShouldSerializeExtension()
	{
		return Extension?.Length > 0;
	}
}
