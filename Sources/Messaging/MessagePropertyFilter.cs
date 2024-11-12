using System;
using System.ComponentModel;

namespace NetCore.Msmq.Messaging;

[TypeConverter(typeof(ExpandableObjectConverter))]
public class MessagePropertyFilter : ICloneable
{
	internal const int ACKNOWLEDGEMENT = 1;
	internal const int ACKNOWLEDGE_TYPE = 1 << 2;
	internal const int ADMIN_QUEUE = 1 << 3;
	internal const int BODY = 1 << 4;
	internal const int LABEL = 1 << 5;
	internal const int ID = 1 << 6;
	internal const int USE_DEADLETTER_QUEUE = 1 << 7;
	internal const int RESPONSE_QUEUE = 1 << 8;
	internal const int MESSAGE_TYPE = 1 << 9;
	internal const int USE_JOURNALING = 1 << 10;
	internal const int LOOKUP_ID = 1 << 11;

	internal const int APP_SPECIFIC = 1;
	internal const int ARRIVED_TIME = 1 << 2;
	internal const int ATTACH_SENDER_ID = 1 << 3;
	internal const int AUTHENTICATED = 1 << 4;
	internal const int CONNECTOR_TYPE = 1 << 5;
	internal const int CORRELATION_ID = 1 << 6;
	internal const int CRYPTOGRAPHIC_PROVIDER_NAME = 1 << 7;
	internal const int CRYPTOGRAPHIC_PROVIDER_TYPE = 1 << 8;
	internal const int IS_RECOVERABLE = 1 << 9;
	internal const int DIGITAL_SIGNATURE = 1 << 10;
	internal const int ENCRYPTION_ALGORITHM = 1 << 11;
	internal const int EXTENSION = 1 << 12;
	internal const int FOREIGN_ADMIN_QUEUE = 1 << 13;
	internal const int HASH_ALGORITHM = 1 << 14;
	internal const int DESTINATION_QUEUE = 1 << 15;
	internal const int PRIORITY = 1 << 16;
	internal const int SECURITY_CONTEXT = 1 << 17;
	internal const int SENDER_CERTIFICATE = 1 << 18;
	internal const int SENDER_ID = 1 << 19;
	internal const int SENT_TIME = 1 << 20;
	internal const int SOURCE_MACHINE = 1 << 21;
	internal const int SYMMETRIC_KEY = 1 << 22;
	internal const int TIME_TO_BE_RECEIVED = 1 << 23;
	internal const int TIME_TO_REACH_QUEUE = 1 << 24;
	internal const int USE_AUTHENTICATION = 1 << 25;
	internal const int USE_ENCRYPTION = 1 << 26;
	internal const int USE_TRACING = 1 << 27;
	internal const int VERSION = 1 << 28;
	internal const int IS_FIRST_IN_TRANSACTION = 1 << 29;
	internal const int IS_LAST_IN_TRANSACTION = 1 << 30;
	internal const int TRANSACTION_ID = 1 << 31;

	internal int data1;
	internal int data2;
	private const int defaultBodySize = 1024;
	private const int defaultExtensionSize = 255;
	private const int defaultLabelSize = 255;
	internal int bodySize = defaultBodySize;
	internal int extensionSize = defaultExtensionSize;
	internal int labelSize = defaultLabelSize;

	[DefaultValue(true), MessagingDescription(Res.MsgAcknowledgement)]
	public bool Acknowledgment
	{
		get => (data1 & ACKNOWLEDGEMENT) != 0;

		set => data1 = value ? data1 | ACKNOWLEDGEMENT : data1 & ~ACKNOWLEDGEMENT;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgAcknowledgeType)]
	public bool AcknowledgeType
	{
		get => (data1 & ACKNOWLEDGE_TYPE) != 0;

		set => data1 = value ? data1 | ACKNOWLEDGE_TYPE : data1 & ~ACKNOWLEDGE_TYPE;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgAdministrationQueue)]
	public bool AdministrationQueue
	{
		get => (data1 & ADMIN_QUEUE) != 0;

		set => data1 = value ? data1 | ADMIN_QUEUE : data1 & ~ADMIN_QUEUE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgAppSpecific)]
	public bool AppSpecific
	{
		get => (data2 & APP_SPECIFIC) != 0;

		set => data2 = value ? data2 | APP_SPECIFIC : data2 & ~APP_SPECIFIC;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgArrivedTime)]
	public bool ArrivedTime
	{
		get => (data2 & ARRIVED_TIME) != 0;

		set => data2 = value ? data2 | ARRIVED_TIME : data2 & ~ARRIVED_TIME;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgAttachSenderId)]
	public bool AttachSenderId
	{
		get => (data2 & ATTACH_SENDER_ID) != 0;

		set => data2 = value ? data2 | ATTACH_SENDER_ID : data2 & ~ATTACH_SENDER_ID;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgAuthenticated)]
	public bool Authenticated
	{
		get => (data2 & AUTHENTICATED) != 0;

		set => data2 = value ? data2 | AUTHENTICATED : data2 & ~AUTHENTICATED;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgAuthenticationProviderName)]
	public bool AuthenticationProviderName
	{
		get => (data2 & CRYPTOGRAPHIC_PROVIDER_NAME) != 0;

		set => data2 = value ? data2 | CRYPTOGRAPHIC_PROVIDER_NAME : data2 & ~CRYPTOGRAPHIC_PROVIDER_NAME;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgAuthenticationProviderType)]
	public bool AuthenticationProviderType
	{
		get => (data2 & CRYPTOGRAPHIC_PROVIDER_TYPE) != 0;

		set => data2 = value ? data2 | CRYPTOGRAPHIC_PROVIDER_TYPE : data2 & ~CRYPTOGRAPHIC_PROVIDER_TYPE;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgBody)]
	public bool Body
	{
		get => (data1 & BODY) != 0;

		set => data1 = value ? data1 | BODY : data1 & ~BODY;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgConnectorType)]
	public bool ConnectorType
	{
		get => (data2 & CONNECTOR_TYPE) != 0;

		set => data2 = value ? data2 | CONNECTOR_TYPE : data2 & ~CONNECTOR_TYPE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgCorrelationId)]
	public bool CorrelationId
	{
		get => (data2 & CORRELATION_ID) != 0;

		set => data2 = value ? data2 | CORRELATION_ID : data2 & ~CORRELATION_ID;
	}

	[DefaultValue(defaultBodySize), MessagingDescription(Res.MsgDefaultBodySize)]
	public int DefaultBodySize
	{
		get => bodySize;

		set
		{
			if (value < 0)
				throw new ArgumentException(Res.GetString(Res.DefaultSizeError));

			bodySize = value;
		}
	}

	[DefaultValue(defaultExtensionSize), MessagingDescription(Res.MsgDefaultExtensionSize)]
	public int DefaultExtensionSize
	{
		get => extensionSize;

		set
		{
			if (value < 0)
				throw new ArgumentException(Res.GetString(Res.DefaultSizeError));

			extensionSize = value;
		}
	}

	[DefaultValue(defaultLabelSize), MessagingDescription(Res.MsgDefaultLabelSize)]
	public int DefaultLabelSize
	{
		get => labelSize;

		set
		{
			if (value < 0)
				throw new ArgumentException(Res.GetString(Res.DefaultSizeError));

			labelSize = value;
		}
	}

	[DefaultValue(false), MessagingDescription(Res.MsgDestinationQueue)]
	public bool DestinationQueue
	{
		get => (data2 & DESTINATION_QUEUE) != 0;

		set => data2 = value ? data2 | DESTINATION_QUEUE : data2 & ~DESTINATION_QUEUE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgDestinationSymmetricKey)]
	public bool DestinationSymmetricKey
	{
		get => (data2 & SYMMETRIC_KEY) != 0;

		set => data2 = value ? data2 | SYMMETRIC_KEY : data2 & ~SYMMETRIC_KEY;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgDigitalSignature)]
	public bool DigitalSignature
	{
		get => (data2 & DIGITAL_SIGNATURE) != 0;

		set => data2 = value ? data2 | DIGITAL_SIGNATURE : data2 & ~DIGITAL_SIGNATURE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgEncryptionAlgorithm)]
	public bool EncryptionAlgorithm
	{
		get => (data2 & ENCRYPTION_ALGORITHM) != 0;

		set => data2 = value ? data2 | ENCRYPTION_ALGORITHM : data2 & ~ENCRYPTION_ALGORITHM;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgExtension)]
	public bool Extension
	{
		get => (data2 & EXTENSION) != 0;

		set => data2 = value ? data2 | EXTENSION : data2 & ~EXTENSION;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgHashAlgorithm)]
	public bool HashAlgorithm
	{
		get => (data2 & HASH_ALGORITHM) != 0;

		set => data2 = value ? data2 | HASH_ALGORITHM : data2 & ~HASH_ALGORITHM;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgId)]
	public bool Id
	{
		get => (data1 & ID) != 0;

		set => data1 = value ? data1 | ID : data1 & ~ID;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgIsFirstInTransaction)]
	public bool IsFirstInTransaction
	{
		get => (data2 & IS_FIRST_IN_TRANSACTION) != 0;

		set => data2 = value ? data2 | IS_FIRST_IN_TRANSACTION : data2 & ~IS_FIRST_IN_TRANSACTION;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgIsLastInTransaction)]
	public bool IsLastInTransaction
	{
		get => (data2 & IS_LAST_IN_TRANSACTION) != 0;

		set => data2 = value ? data2 | IS_LAST_IN_TRANSACTION : data2 & ~IS_LAST_IN_TRANSACTION;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgLabel)]
	public bool Label
	{
		get => (data1 & LABEL) != 0;

		set => data1 = value ? data1 | LABEL : data1 & ~LABEL;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgLookupId)]
	public bool LookupId
	{
		get
		{
			if (!MessageQueue.Msmq3OrNewer)
				throw new PlatformNotSupportedException(Res.GetString(Res.PlatformNotSupported));

			return (data1 & LOOKUP_ID) != 0;
		}

		set
		{
			if (!MessageQueue.Msmq3OrNewer)
				throw new PlatformNotSupportedException(Res.GetString(Res.PlatformNotSupported));

			data1 = value ? data1 | LOOKUP_ID : data1 & ~LOOKUP_ID;
		}
	}

	[DefaultValue(true), MessagingDescription(Res.MsgMessageType)]
	public bool MessageType
	{
		get => (data1 & MESSAGE_TYPE) != 0;

		set => data1 = value ? data1 | MESSAGE_TYPE : data1 & ~MESSAGE_TYPE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgPriority)]
	public bool Priority
	{
		get => (data2 & PRIORITY) != 0;

		set => data2 = value ? data2 | PRIORITY : data2 & ~PRIORITY;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgRecoverable)]
	public bool Recoverable
	{
		get => (data2 & IS_RECOVERABLE) != 0;

		set => data2 = value ? data2 | IS_RECOVERABLE : data2 & ~IS_RECOVERABLE;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgResponseQueue)]
	public bool ResponseQueue
	{
		get => (data1 & RESPONSE_QUEUE) != 0;

		set => data1 = value ? data1 | RESPONSE_QUEUE : data1 & ~RESPONSE_QUEUE;
	}

	// SecurityContext is send-only property, so there's no point in 
	// publicly exposing it in the filter
	internal bool SecurityContext
	{
		get => (data2 & SECURITY_CONTEXT) != 0;

		set => data2 = value ? data2 | SECURITY_CONTEXT : data2 & ~SECURITY_CONTEXT;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgSenderCertificate)]
	public bool SenderCertificate
	{
		get => (data2 & SENDER_CERTIFICATE) != 0;

		set => data2 = value ? data2 | SENDER_CERTIFICATE : data2 & ~SENDER_CERTIFICATE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgSenderId)]
	public bool SenderId
	{
		get => (data2 & SENDER_ID) != 0;

		set => data2 = value ? data2 | SENDER_ID : data2 & ~SENDER_ID;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgSenderVersion)]
	public bool SenderVersion
	{
		get => (data2 & VERSION) != 0;

		set => data2 = value ? data2 | VERSION : data2 & ~VERSION;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgSentTime)]
	public bool SentTime
	{
		get => (data2 & SENT_TIME) != 0;

		set => data2 = value ? data2 | SENT_TIME : data2 & ~SENT_TIME;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgSourceMachine)]
	public bool SourceMachine
	{
		get => (data2 & SOURCE_MACHINE) != 0;

		set => data2 = value ? data2 | SOURCE_MACHINE : data2 & ~SOURCE_MACHINE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgTimeToBeReceived)]
	public bool TimeToBeReceived
	{
		get => (data2 & TIME_TO_BE_RECEIVED) != 0;

		set => data2 = value ? data2 | TIME_TO_BE_RECEIVED : data2 & ~TIME_TO_BE_RECEIVED;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgTimeToReachQueue)]
	public bool TimeToReachQueue
	{
		get => (data2 & TIME_TO_REACH_QUEUE) != 0;

		set => data2 = value ? data2 | TIME_TO_REACH_QUEUE : data2 & ~TIME_TO_REACH_QUEUE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgTransactionId)]
	public bool TransactionId
	{
		get => (data2 & TRANSACTION_ID) != 0;

		set => data2 = value ? data2 | TRANSACTION_ID : data2 & ~TRANSACTION_ID;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgTransactionStatusQueue)]
	public bool TransactionStatusQueue
	{
		get => (data2 & FOREIGN_ADMIN_QUEUE) != 0;

		set => data2 = value ? data2 | FOREIGN_ADMIN_QUEUE : data2 & ~FOREIGN_ADMIN_QUEUE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseAuthentication)]
	public bool UseAuthentication
	{
		get => (data2 & USE_AUTHENTICATION) != 0;

		set => data2 = value ? data2 | USE_AUTHENTICATION : data2 & ~USE_AUTHENTICATION;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgUseDeadLetterQueue)]
	public bool UseDeadLetterQueue
	{
		get => (data1 & USE_DEADLETTER_QUEUE) != 0;

		set => data1 = value ? data1 | USE_DEADLETTER_QUEUE : data1 & ~USE_DEADLETTER_QUEUE;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseEncryption)]
	public bool UseEncryption
	{
		get => (data2 & USE_ENCRYPTION) != 0;

		set => data2 = value ? data2 | USE_ENCRYPTION : data2 & ~USE_ENCRYPTION;
	}

	[DefaultValue(true), MessagingDescription(Res.MsgUseJournalQueue)]
	public bool UseJournalQueue
	{
		get => (data1 & USE_JOURNALING) != 0;

		set => data1 = value ? data1 | USE_JOURNALING : data2 & ~USE_JOURNALING;
	}

	[DefaultValue(false), MessagingDescription(Res.MsgUseTracing)]
	public bool UseTracing
	{
		get => (data2 & USE_TRACING) != 0;

		set => data2 = value ? data2 | USE_TRACING : data2 & ~USE_TRACING;
	}

	public void ClearAll()
	{
		data1 = 0;
		data2 = 0;
	}

	public void SetDefaults()
	{
		data1 = ACKNOWLEDGEMENT |
			   ACKNOWLEDGE_TYPE |
			   ADMIN_QUEUE |
			   BODY |
			   ID |
			   LABEL |
			   USE_DEADLETTER_QUEUE |
			   RESPONSE_QUEUE |
			   MESSAGE_TYPE |
			   USE_JOURNALING |
			   LOOKUP_ID;

		data2 = 0;
		DefaultBodySize = defaultBodySize;
		DefaultExtensionSize = defaultExtensionSize;
		DefaultLabelSize = defaultLabelSize;
	}

	public void SetAll()
	{
		data1 = ACKNOWLEDGEMENT |
			   ACKNOWLEDGE_TYPE |
			   ADMIN_QUEUE |
			   BODY |
			   ID |
			   LABEL |
			   USE_DEADLETTER_QUEUE |
			   RESPONSE_QUEUE |
			   MESSAGE_TYPE |
			   USE_JOURNALING |
			   LOOKUP_ID;

		data2 = APP_SPECIFIC |
			   ARRIVED_TIME |
			   ATTACH_SENDER_ID |
			   AUTHENTICATED |
			   CONNECTOR_TYPE |
			   CORRELATION_ID |
			   CRYPTOGRAPHIC_PROVIDER_NAME |
			   CRYPTOGRAPHIC_PROVIDER_TYPE |
			   IS_RECOVERABLE |
			   DESTINATION_QUEUE |
			   DIGITAL_SIGNATURE |
			   ENCRYPTION_ALGORITHM |
			   EXTENSION |
			   FOREIGN_ADMIN_QUEUE |
			   HASH_ALGORITHM |
			   PRIORITY |
			   SECURITY_CONTEXT |
			   SENDER_CERTIFICATE |
			   SENDER_ID |
			   SENT_TIME |
			   SOURCE_MACHINE |
			   SYMMETRIC_KEY |
			   TIME_TO_BE_RECEIVED |
			   TIME_TO_REACH_QUEUE |
			   USE_AUTHENTICATION |
			   USE_ENCRYPTION |
			   USE_TRACING |
			   VERSION |
			   IS_FIRST_IN_TRANSACTION |
			   IS_LAST_IN_TRANSACTION |
			   TRANSACTION_ID;
	}

	public virtual object Clone()
	{
		return MemberwiseClone();
	}
}
