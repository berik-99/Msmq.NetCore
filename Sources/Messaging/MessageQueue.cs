using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.DirectoryServices;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Transactions;
using Msmq.NetCore.Messaging.Design;
using Msmq.NetCore.Messaging.Interop;

namespace Msmq.NetCore.Messaging;

[DefaultEvent("ReceiveCompleted"),
TypeConverter(typeof(MessageQueueConverter)),
Editor("System.Messaging.Design.QueuePathEditor", "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
MessagingDescription(Res.MessageQueueDesc)]
[SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix")]
public class MessageQueue : Component, IEnumerable
{
	//Public constants
	public static readonly TimeSpan InfiniteTimeout = TimeSpan.FromMilliseconds(uint.MaxValue);
	public static readonly long InfiniteQueueSize = uint.MaxValue;

	//Internal members

	private DefaultPropertiesToSend defaultProperties;
	private MessagePropertyFilter receiveFilter;
	private int sharedMode;
	private string formatName;
	private string queuePath;
	private string path;
	private readonly bool enableCache;
	private QueuePropertyVariants properties;
	private IMessageFormatter formatter;

	// Double-checked locking pattern requires volatile for read/write synchronization
	private static volatile string computerName;

	internal static readonly Version OSVersion = Environment.OSVersion.Version;
	internal static readonly Version WinXP = new Version(5, 1);
	internal static readonly bool Msmq3OrNewer = OSVersion >= WinXP;

	//Cached properties
	private QueuePropertyFilter filter;
	private bool authenticate;
	private short basePriority;
	private DateTime createTime;
	private int encryptionLevel;
	private Guid id;
	private string label;
	private string multicastAddress;
	private DateTime lastModifyTime;
	private long journalSize;
	private long queueSize;
	private Guid queueType;
	private bool useJournaling;
	private MQCacheableInfo mqInfo;

	// Double-checked locking pattern requires volatile for read/write synchronization
	//Async IO support        
	private volatile bool attached;
	private bool useThreadPool;
	private AsyncCallback onRequestCompleted;
	private PeekCompletedEventHandler onPeekCompleted;
	private ReceiveCompletedEventHandler onReceiveCompleted;
	private ISynchronizeInvoke synchronizingObject;

	// Double-checked locking pattern requires volatile for read/write synchronization
	private volatile Hashtable outstandingAsyncRequests;

	//Path sufixes
	private const string SUFIX_PRIVATE = "\\PRIVATE$";
	private const string SUFIX_JOURNAL = "\\JOURNAL$";
	private const string SUFIX_DEADLETTER = "\\DEADLETTER$";
	private const string SUFIX_DEADXACT = "\\XACTDEADLETTER$";

	//Path prefixes
	private const string PREFIX_LABEL = "LABEL:";
	private const string PREFIX_FORMAT_NAME = "FORMATNAME:";

	//Connection pooling support
	private static readonly CacheTable<string, string> formatNameCache =
		new CacheTable<string, string>("formatNameCache", 4, new TimeSpan(0, 0, 100));   // path -> formatname

	private static readonly CacheTable<QueueInfoKeyHolder, MQCacheableInfo> queueInfoCache =
		new CacheTable<QueueInfoKeyHolder, MQCacheableInfo>("queue info", 4, new TimeSpan(0, 0, 100));        // <formatname, accessMode> -> <readHandle. writeHandle, isTrans>

	// Double-checked locking pattern requires volatile for read/write synchronization
	private volatile QueueInfoKeyHolder queueInfoKey = null;

	//Code Acess Security support            
	private bool administerGranted;
	private bool browseGranted;
	private bool sendGranted;
	private bool receiveGranted;
	private bool peekGranted;

	private readonly object syncRoot = new object();
	private static readonly object staticSyncRoot = new object();

	static MessageQueue()
	{
		try
		{
			using (TelemetryEventSource eventSource = new TelemetryEventSource())
			{
				eventSource.MessageQueue();
			}
		}
		catch
		{
		}
	}

	//
	public MessageQueue()
	{
		path = string.Empty;
		AccessMode = QueueAccessMode.SendAndReceive;
	}

	public MessageQueue(string path)
		: this(path, false, EnableConnectionCache)
	{
	}

	public MessageQueue(string path, QueueAccessMode accessMode)
		: this(path, false, EnableConnectionCache, accessMode)
	{
	}

	public MessageQueue(string path, bool sharedModeDenyReceive)
		: this(path, sharedModeDenyReceive, EnableConnectionCache)
	{
	}

	public MessageQueue(string path, bool sharedModeDenyReceive, bool enableCache)
	{
		this.path = path;
		this.enableCache = enableCache;
		if (sharedModeDenyReceive)
		{
			sharedMode = NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE;
		}
		AccessMode = QueueAccessMode.SendAndReceive;
	}

	public MessageQueue(string path, bool sharedModeDenyReceive,
						bool enableCache, QueueAccessMode accessMode)
	{
		this.path = path;
		this.enableCache = enableCache;
		if (sharedModeDenyReceive)
		{
			sharedMode = NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE;
		}
		SetAccessMode(accessMode);
	}

	internal MessageQueue(string path, Guid id)
	{
		PropertyFilter.Id = true;
		this.id = id;
		this.path = path;
		AccessMode = QueueAccessMode.SendAndReceive;
	}

	public QueueAccessMode AccessMode { get; private set; }

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_Authenticate)]
	public bool Authenticate
	{
		get
		{
			if (!PropertyFilter.Authenticate)
			{
				Properties.SetUI1(NativeMethods.QUEUE_PROPID_AUTHENTICATE, 0);
				GenerateQueueProperties();
				authenticate = Properties.GetUI1(NativeMethods.QUEUE_PROPID_AUTHENTICATE) != NativeMethods.QUEUE_AUTHENTICATE_NONE;
				PropertyFilter.Authenticate = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_AUTHENTICATE);
			}

			return authenticate;
		}

		set
		{
			if (value)
				Properties.SetUI1(NativeMethods.QUEUE_PROPID_AUTHENTICATE, NativeMethods.QUEUE_AUTHENTICATE_AUTHENTICATE);
			else
				Properties.SetUI1(NativeMethods.QUEUE_PROPID_AUTHENTICATE, NativeMethods.QUEUE_AUTHENTICATE_NONE);

			SaveQueueProperties();
			authenticate = value;
			PropertyFilter.Authenticate = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_AUTHENTICATE);
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_BasePriority)]
	public short BasePriority
	{
		get
		{
			if (!PropertyFilter.BasePriority)
			{
				Properties.SetI2(NativeMethods.QUEUE_PROPID_BASEPRIORITY, 0);
				GenerateQueueProperties();
				basePriority = properties.GetI2(NativeMethods.QUEUE_PROPID_BASEPRIORITY);
				PropertyFilter.BasePriority = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_BASEPRIORITY);
			}

			return basePriority;
		}

		set
		{
			Properties.SetI2(NativeMethods.QUEUE_PROPID_BASEPRIORITY, value);
			SaveQueueProperties();
			basePriority = value;
			PropertyFilter.BasePriority = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_BASEPRIORITY);
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_CanRead)]
	public bool CanRead
	{
		get
		{
			if (!browseGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				browseGranted = true;
			}

			return MQInfo.CanRead;
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_CanWrite)]
	public bool CanWrite
	{
		get
		{
			if (!browseGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				browseGranted = true;
			}

			return MQInfo.CanWrite;
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_Category)]
	public Guid Category
	{
		get
		{
			if (!PropertyFilter.Category)
			{
				Properties.SetNull(NativeMethods.QUEUE_PROPID_TYPE);
				GenerateQueueProperties();
				byte[] bytes = new byte[16];
				IntPtr handle = Properties.GetIntPtr(NativeMethods.QUEUE_PROPID_TYPE);
				if (handle != IntPtr.Zero)
				{
					Marshal.Copy(handle, bytes, 0, 16);
					//MSMQ allocated memory for this operation, needs to be freed
					SafeNativeMethods.MQFreeMemory(handle);
				}

				queueType = new Guid(bytes);
				PropertyFilter.Category = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_TYPE);
			}
			return queueType;
		}

		set
		{
			Properties.SetGuid(NativeMethods.QUEUE_PROPID_TYPE, value.ToByteArray());
			SaveQueueProperties();
			queueType = value;
			PropertyFilter.Category = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_TYPE);
		}
	}

	internal static string ComputerName
	{
		get
		{
			if (computerName == null)
			{
				lock (staticSyncRoot)
				{
					if (computerName == null)
					{
						StringBuilder sb = new StringBuilder(256);
						SafeNativeMethods.GetComputerName(sb, new int[] { sb.Capacity });
						computerName = sb.ToString();
					}
				}
			}

			return computerName;
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_CreateTime)]
	public DateTime CreateTime
	{
		get
		{
			if (!PropertyFilter.CreateTime)
			{
				DateTime time = new DateTime(1970, 1, 1);
				Properties.SetI4(NativeMethods.QUEUE_PROPID_CREATE_TIME, 0);
				GenerateQueueProperties();
				createTime = time.AddSeconds(properties.GetI4(NativeMethods.QUEUE_PROPID_CREATE_TIME)).ToLocalTime();
				PropertyFilter.CreateTime = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_CREATE_TIME);
			}

			return createTime;
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Content), MessagingDescription(Res.MQ_DefaultPropertiesToSend)]
	public DefaultPropertiesToSend DefaultPropertiesToSend
	{
		get
		{
			if (defaultProperties == null)
			{
				if (DesignMode)
					defaultProperties = new DefaultPropertiesToSend(true);
				else
					defaultProperties = new DefaultPropertiesToSend();
			}

			return defaultProperties;
		}

		set => defaultProperties = value;
	}

	[Browsable(false), DefaultValue(false), MessagingDescription(Res.MQ_DenySharedReceive)]
	public bool DenySharedReceive
	{
		get => sharedMode == NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE;
		set
		{
			if (value && sharedMode != NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE)
			{
				Close();
				sharedMode = NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE;
			}
			else if (!value && sharedMode == NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE)
			{
				Close();
				sharedMode = NativeMethods.QUEUE_SHARED_MODE_DENY_NONE;
			}
		}
	}

	[Browsable(false)]
	public static bool EnableConnectionCache { get; set; }

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_EncryptionRequired)]
	public EncryptionRequired EncryptionRequired
	{
		get
		{
			if (!PropertyFilter.EncryptionLevel)
			{
				Properties.SetUI4(NativeMethods.QUEUE_PROPID_PRIV_LEVEL, 0);
				GenerateQueueProperties();
				encryptionLevel = Properties.GetUI4(NativeMethods.QUEUE_PROPID_PRIV_LEVEL);
				PropertyFilter.EncryptionLevel = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_PRIV_LEVEL);
			}
			return (EncryptionRequired)encryptionLevel;
		}

		set
		{
			if (!ValidationUtility.ValidateEncryptionRequired(value))
				throw new InvalidEnumArgumentException("value", (int)value, typeof(EncryptionRequired));

			Properties.SetUI4(NativeMethods.QUEUE_PROPID_PRIV_LEVEL, (int)value);
			SaveQueueProperties();
			encryptionLevel = properties.GetUI4(NativeMethods.QUEUE_PROPID_PRIV_LEVEL);
			PropertyFilter.EncryptionLevel = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_PRIV_LEVEL);
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_FormatName)]
	public string FormatName
	{
		get
		{
			if (formatName == null)
			{
				if (string.IsNullOrEmpty(path))
				{
					return string.Empty;
				}

				string pathUpper = path.ToUpper(CultureInfo.InvariantCulture);

				// see if we already have this cached 
				if (enableCache)
					formatName = formatNameCache.Get(pathUpper);

				// not in the cache?  keep working.
				if (formatName == null)
				{
					if (PropertyFilter.Id)
					{
						//Improves performance when enumerating queues.
						//This codepath will only be executed when accessing
						//a queue returned by MessageQueueEnumerator.                        
						int result;
						int status = 0;
						StringBuilder newFormatName = new StringBuilder(NativeMethods.MAX_LABEL_LEN);
						result = NativeMethods.MAX_LABEL_LEN;
						status = SafeNativeMethods.MQInstanceToFormatName(id.ToByteArray(), newFormatName, ref result);
						if (status != 0)
							throw new MessageQueueException(status);

						formatName = newFormatName.ToString();
						return formatName;
					}

					if (pathUpper.StartsWith(PREFIX_FORMAT_NAME))
					{
						formatName = path.Substring(PREFIX_FORMAT_NAME.Length);
					}
					else if (pathUpper.StartsWith(PREFIX_LABEL))
					{
						MessageQueue labeledQueue = ResolveQueueFromLabel(path, true);
						formatName = labeledQueue.FormatName;
						queuePath = labeledQueue.QueuePath;
					}
					else
					{
						queuePath = path;
						formatName = ResolveFormatNameFromQueuePath(queuePath, true);
					}

					formatNameCache.Put(pathUpper, formatName);
				}
			}

			return formatName;
		}
	}

	[DefaultValue(null),
	TypeConverter(typeof(MessageFormatterConverter)),
	Browsable(false),
	MessagingDescription(Res.MQ_Formatter)]
	public IMessageFormatter Formatter
	{
		get
		{
			if (formatter == null && !DesignMode)
				formatter = new XmlMessageFormatter();
			return formatter;
		}

		set => formatter = value;
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_GuidId)]
	public Guid Id
	{
		get
		{
			if (!PropertyFilter.Id)
			{
				Properties.SetNull(NativeMethods.QUEUE_PROPID_INSTANCE);
				GenerateQueueProperties();
				byte[] bytes = new byte[16];
				IntPtr handle = Properties.GetIntPtr(NativeMethods.QUEUE_PROPID_INSTANCE);
				if (handle != IntPtr.Zero)
				{
					Marshal.Copy(handle, bytes, 0, 16);
					//MSMQ allocated memory for this operation, needs to be freed
					SafeNativeMethods.MQFreeMemory(handle);
				}
				id = new Guid(bytes);
				PropertyFilter.Id = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_INSTANCE);
			}
			return id;
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_Label)]
	public string Label
	{
		get
		{
			if (!PropertyFilter.Label)
			{
				Properties.SetNull(NativeMethods.QUEUE_PROPID_LABEL);
				GenerateQueueProperties();
				string description = null;
				IntPtr handle = Properties.GetIntPtr(NativeMethods.QUEUE_PROPID_LABEL);
				if (handle != IntPtr.Zero)
				{
					//Using Unicode API even on Win9x
					description = Marshal.PtrToStringUni(handle);
					//MSMQ allocated memory for this operation, needs to be freed
					SafeNativeMethods.MQFreeMemory(handle);
				}

				label = description;
				PropertyFilter.Label = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_LABEL);
			}

			return label;
		}

		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			//Borrow this function from message
			Properties.SetString(NativeMethods.QUEUE_PROPID_LABEL, Message.StringToBytes(value));
			SaveQueueProperties();
			label = value;
			PropertyFilter.Label = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_LABEL);
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_LastModifyTime)]
	public DateTime LastModifyTime
	{
		get
		{
			if (!PropertyFilter.LastModifyTime)
			{
				DateTime time = new DateTime(1970, 1, 1);
				Properties.SetI4(NativeMethods.QUEUE_PROPID_MODIFY_TIME, 0);
				GenerateQueueProperties();
				lastModifyTime = time.AddSeconds(properties.GetI4(NativeMethods.QUEUE_PROPID_MODIFY_TIME)).ToLocalTime();
				PropertyFilter.LastModifyTime = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_MODIFY_TIME);
			}

			return lastModifyTime;
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_MachineName)]
	public string MachineName
	{
		get
		{
			string queuePath = QueuePath;
			if (queuePath.Length == 0)
			{
				return queuePath;
			}
			return queuePath.Substring(0, queuePath.IndexOf('\\'));
		}

		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			if (!SyntaxCheck.CheckMachineName(value))
				throw new ArgumentException(Res.GetString(Res.InvalidProperty, "MachineName", value));

			StringBuilder newPath = new StringBuilder();
			if (string.IsNullOrEmpty(path) && formatName == null)
			{
				//Need to default to an existing queue, for instance Journal.
				newPath.Append(value);
				newPath.Append(SUFIX_JOURNAL);
			}
			else
			{
				newPath.Append(value);
				newPath.Append('\\');
				newPath.Append(QueueName);
			}
			Path = newPath.ToString();
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
	MessagingDescription(Res.MQ_MaximumJournalSize),
	TypeConverter(typeof(SizeConverter))]
	public long MaximumJournalSize
	{
		get
		{
			if (!PropertyFilter.MaximumJournalSize)
			{
				Properties.SetUI4(NativeMethods.QUEUE_PROPID_JOURNAL_QUOTA, 0);
				GenerateQueueProperties();
				journalSize = (uint)properties.GetUI4(NativeMethods.QUEUE_PROPID_JOURNAL_QUOTA);
				PropertyFilter.MaximumJournalSize = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_JOURNAL_QUOTA);
			}

			return journalSize;
		}

		set
		{
			if (value > InfiniteQueueSize || value < 0)
				throw new ArgumentException(Res.GetString(Res.InvalidProperty, "MaximumJournalSize", value));

			Properties.SetUI4(NativeMethods.QUEUE_PROPID_JOURNAL_QUOTA, (int)(uint)value);
			SaveQueueProperties();
			journalSize = value;
			PropertyFilter.MaximumJournalSize = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_JOURNAL_QUOTA);
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
	MessagingDescription(Res.MQ_MaximumQueueSize),
	TypeConverter(typeof(SizeConverter))]
	public long MaximumQueueSize
	{
		get
		{
			if (!PropertyFilter.MaximumQueueSize)
			{
				Properties.SetUI4(NativeMethods.QUEUE_PROPID_QUOTA, 0);
				GenerateQueueProperties();
				queueSize = (uint)properties.GetUI4(NativeMethods.QUEUE_PROPID_QUOTA);
				PropertyFilter.MaximumQueueSize = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_QUOTA);
			}

			return queueSize;
		}

		set
		{
			if (value > InfiniteQueueSize || value < 0)
				throw new ArgumentException(Res.GetString(Res.InvalidProperty, "MaximumQueueSize", value));

			Properties.SetUI4(NativeMethods.QUEUE_PROPID_QUOTA, (int)(uint)value);
			SaveQueueProperties();
			queueSize = value;
			PropertyFilter.MaximumQueueSize = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_QUOTA);
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Content), MessagingDescription(Res.MQ_MessageReadPropertyFilter)]
	public MessagePropertyFilter MessageReadPropertyFilter
	{
		get
		{
			if (receiveFilter == null)
			{
				receiveFilter = new MessagePropertyFilter();
				receiveFilter.SetDefaults();
			}

			return receiveFilter;
		}

		set => receiveFilter = value ?? throw new ArgumentNullException(nameof(value));
	}

	internal MQCacheableInfo MQInfo
	{
		get
		{
			if (mqInfo == null)
			{
				MQCacheableInfo cachedInfo = queueInfoCache.Get(QueueInfoKey);
				if (sharedMode == NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE || !enableCache)
				{
					cachedInfo?.CloseIfNotReferenced();

					// don't use the cache
					mqInfo = new MQCacheableInfo(FormatName, AccessMode, sharedMode);
					mqInfo.AddRef();
				}
				else
				{
					// use the cache                        
					if (cachedInfo != null)
					{
						cachedInfo.AddRef();
						mqInfo = cachedInfo;
					}
					else
					{
						mqInfo = new MQCacheableInfo(FormatName, AccessMode, sharedMode);
						mqInfo.AddRef();
						queueInfoCache.Put(QueueInfoKey, mqInfo);
					}
				}
			}

			return mqInfo;
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
	 DefaultValue(""),
	 MessagingDescription(Res.MQ_MulticastAddress)]
	public string MulticastAddress
	{
		get
		{
			if (!Msmq3OrNewer)
			{ //this feature is unavailable on win2k
			  // don't throw in design mode: this makes component unusable
				if (DesignMode)
					return string.Empty;
				else
					throw new PlatformNotSupportedException(Res.GetString(Res.PlatformNotSupported));
			}

			if (!PropertyFilter.MulticastAddress)
			{
				Properties.SetNull(NativeMethods.QUEUE_PROPID_MULTICAST_ADDRESS);
				GenerateQueueProperties();
				string address = null;
				IntPtr handle = Properties.GetIntPtr(NativeMethods.QUEUE_PROPID_MULTICAST_ADDRESS);
				if (handle != IntPtr.Zero)
				{
					address = Marshal.PtrToStringUni(handle);
					//MSMQ allocated memory for this operation, needs to be freed
					SafeNativeMethods.MQFreeMemory(handle);
				}

				multicastAddress = address;
				PropertyFilter.MulticastAddress = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_MULTICAST_ADDRESS);
			}

			return multicastAddress;
		}
		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			if (!Msmq3OrNewer) //this feature is unavailable on win2k
				throw new PlatformNotSupportedException(Res.GetString(Res.PlatformNotSupported));

			if (value.Length == 0) // used to disassocciate queue from a muliticast address
				Properties.SetEmpty(NativeMethods.QUEUE_PROPID_MULTICAST_ADDRESS);
			else //Borrow this function from message
				Properties.SetString(NativeMethods.QUEUE_PROPID_MULTICAST_ADDRESS, Message.StringToBytes(value));

			SaveQueueProperties();
			multicastAddress = value;
			PropertyFilter.MulticastAddress = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_MULTICAST_ADDRESS);
		}
	}

	[Editor("System.Messaging.Design.QueuePathEditor", "System.Drawing.Design.UITypeEditor, " + AssemblyRef.SystemDrawing),
	 SettingsBindable(true),
	 RefreshProperties(RefreshProperties.All),
	 Browsable(false),
	 DefaultValue(""),
	 TypeConverter("System.Diagnostics.Design.StringValueConverter, " + AssemblyRef.SystemDesign),
	 MessagingDescription(Res.MQ_Path)]
	public string Path
	{
		get => path;

		set
		{
			if (value == null)
				value = string.Empty;

			if (!ValidatePath(value, false))
				throw new ArgumentException(Res.GetString(Res.PathSyntax));

			if (!string.IsNullOrEmpty(path))
				Close();

			path = value;
		}
	}

	private QueuePropertyVariants Properties => properties ?? (properties = new QueuePropertyVariants());

	private QueuePropertyFilter PropertyFilter => filter ?? (filter = new QueuePropertyFilter());

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_QueueName)]
	public string QueueName
	{
		get
		{
			string queuePath = QueuePath;
			if (queuePath.Length == 0)
			{
				return queuePath;
			}
			return queuePath.Substring(queuePath.IndexOf('\\') + 1);
		}

		set
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			StringBuilder newPath = new StringBuilder();
			if (string.IsNullOrEmpty(path) && formatName == null)
			{
				newPath.Append(".\\");
				newPath.Append(value);
			}
			else
			{
				newPath.Append(MachineName);
				newPath.Append("\\");
				newPath.Append(value);
			}
			Path = newPath.ToString();
		}
	}

	internal string QueuePath
	{
		get
		{
			if (queuePath == null)
			{
				if (string.IsNullOrEmpty(path))
				{
					return string.Empty;
				}

				string pathUpper = path.ToUpper(CultureInfo.InvariantCulture);
				if (pathUpper.StartsWith(PREFIX_LABEL))
				{
					MessageQueue labeledQueue = ResolveQueueFromLabel(path, true);
					formatName = labeledQueue.FormatName;
					queuePath = labeledQueue.QueuePath;
				}
				else if (pathUpper.StartsWith(PREFIX_FORMAT_NAME))
				{
					Properties.SetNull(NativeMethods.QUEUE_PROPID_PATHNAME);
					GenerateQueueProperties();
					string description = null;
					IntPtr handle = Properties.GetIntPtr(NativeMethods.QUEUE_PROPID_PATHNAME);
					if (handle != IntPtr.Zero)
					{
						//Using Unicode API even on Win9x
						description = Marshal.PtrToStringUni(handle);
						//MSMQ allocated memory for this operation, needs to be freed
						SafeNativeMethods.MQFreeMemory(handle);
					}
					Properties.Remove(NativeMethods.QUEUE_PROPID_PATHNAME);
					queuePath = description;
				}
				else
				{
					queuePath = path;
				}
			}
			return queuePath;
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_ReadHandle)]
	public IntPtr ReadHandle
	{
		get
		{
			if (!receiveGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Receive, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				receiveGranted = true;
			}

			return MQInfo.ReadHandle.DangerousGetHandle();
		}
	}

	[Browsable(false), DefaultValue(null), MessagingDescription(Res.MQ_SynchronizingObject)]
	public ISynchronizeInvoke SynchronizingObject
	{
		get
		{
			if (synchronizingObject == null && DesignMode)
			{
				IDesignerHost host = (IDesignerHost)GetService(typeof(IDesignerHost));
				if (host != null)
				{
					object baseComponent = host.RootComponent;
					if (baseComponent != null && baseComponent is ISynchronizeInvoke invoke)
						synchronizingObject = invoke;
				}
			}

			return synchronizingObject;
		}

		set => synchronizingObject = value;
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_Transactional)]
	public bool Transactional
	{
		get
		{
			if (!browseGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				browseGranted = true;
			}

			return MQInfo.Transactional;
		}
	}

	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_UseJournalQueue)]
	public bool UseJournalQueue
	{
		get
		{
			if (!PropertyFilter.UseJournalQueue)
			{
				Properties.SetUI1(NativeMethods.QUEUE_PROPID_JOURNAL, 0);
				GenerateQueueProperties();
				useJournaling = Properties.GetUI1(NativeMethods.QUEUE_PROPID_JOURNAL) != NativeMethods.QUEUE_JOURNAL_NONE;
				PropertyFilter.UseJournalQueue = true;
				Properties.Remove(NativeMethods.QUEUE_PROPID_JOURNAL);
			}
			return useJournaling;
		}

		set
		{
			if (value)
				Properties.SetUI1(NativeMethods.QUEUE_PROPID_JOURNAL, NativeMethods.QUEUE_JOURNAL_JOURNAL);
			else
				Properties.SetUI1(NativeMethods.QUEUE_PROPID_JOURNAL, NativeMethods.QUEUE_JOURNAL_NONE);

			SaveQueueProperties();
			useJournaling = value;
			PropertyFilter.UseJournalQueue = true;
			Properties.Remove(NativeMethods.QUEUE_PROPID_JOURNAL);
		}
	}

	[Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), MessagingDescription(Res.MQ_WriteHandle)]
	public IntPtr WriteHandle
	{
		get
		{
			if (!sendGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Send, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				sendGranted = true;
			}

			return MQInfo.WriteHandle.DangerousGetHandle();
		}
	}

	[MessagingDescription(Res.MQ_PeekCompleted)]
	public event PeekCompletedEventHandler PeekCompleted
	{
		add
		{
			if (!peekGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Peek, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				peekGranted = true;
			}

			onPeekCompleted += value;
		}
		remove => onPeekCompleted -= value;
	}

	[MessagingDescription(Res.MQ_ReceiveCompleted)]
	public event ReceiveCompletedEventHandler ReceiveCompleted
	{
		add
		{
			if (!receiveGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Receive, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				receiveGranted = true;
			}

			onReceiveCompleted += value;
		}
		remove => onReceiveCompleted -= value;
	}

	private Hashtable OutstandingAsyncRequests
	{
		get
		{
			if (outstandingAsyncRequests == null)
			{
				lock (syncRoot)
				{
					if (outstandingAsyncRequests == null)
					{
						Hashtable requests = Hashtable.Synchronized(new Hashtable());
						Thread.MemoryBarrier();
						outstandingAsyncRequests = requests;
					}
				}
			}

			return outstandingAsyncRequests;
		}
	}

	private QueueInfoKeyHolder QueueInfoKey
	{
		get
		{
			if (queueInfoKey == null)
			{
				lock (syncRoot)
				{
					if (queueInfoKey == null)
					{
						QueueInfoKeyHolder keyHolder = new QueueInfoKeyHolder(FormatName, AccessMode);
						Thread.MemoryBarrier();
						queueInfoKey = keyHolder;
					}
				}
			}

			return queueInfoKey;
		}
	}

	public IAsyncResult BeginPeek()
	{
		return ReceiveAsync(InfiniteTimeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, null, null);
	}

	public IAsyncResult BeginPeek(TimeSpan timeout)
	{
		return ReceiveAsync(timeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, null, null);
	}

	public IAsyncResult BeginPeek(TimeSpan timeout, object stateObject)
	{
		return ReceiveAsync(timeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, null, stateObject);
	}

	public IAsyncResult BeginPeek(TimeSpan timeout, object stateObject, AsyncCallback callback)
	{
		return ReceiveAsync(timeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, callback, stateObject);
	}

	public IAsyncResult BeginPeek(TimeSpan timeout, Cursor cursor, PeekAction action, object state, AsyncCallback callback)
	{
		if (action != PeekAction.Current && action != PeekAction.Next)
			throw new ArgumentOutOfRangeException(Res.GetString(Res.InvalidParameter, "action", action.ToString()));

		if (cursor == null)
			throw new ArgumentNullException(nameof(cursor));

		return ReceiveAsync(timeout, cursor.Handle, (int)action, callback, state);
	}

	public IAsyncResult BeginReceive()
	{
		return ReceiveAsync(InfiniteTimeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_RECEIVE, null, null);
	}

	public IAsyncResult BeginReceive(TimeSpan timeout)
	{
		return ReceiveAsync(timeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_RECEIVE, null, null);
	}

	public IAsyncResult BeginReceive(TimeSpan timeout, object stateObject)
	{
		return ReceiveAsync(timeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_RECEIVE, null, stateObject);
	}

	public IAsyncResult BeginReceive(TimeSpan timeout, object stateObject, AsyncCallback callback)
	{
		return ReceiveAsync(timeout, CursorHandle.NullHandle, NativeMethods.QUEUE_ACTION_RECEIVE, callback, stateObject);
	}

	public IAsyncResult BeginReceive(TimeSpan timeout, Cursor cursor, object state, AsyncCallback callback)
	{
		if (cursor == null)
			throw new ArgumentNullException(nameof(cursor));

		return ReceiveAsync(timeout, cursor.Handle, NativeMethods.QUEUE_ACTION_RECEIVE, callback, state);
	}

	public static void ClearConnectionCache()
	{
		formatNameCache.ClearStale(new TimeSpan(0));
		queueInfoCache.ClearStale(new TimeSpan(0));
	}

	public void Close()
	{
		Cleanup(true);
	}

	private void Cleanup(bool disposing)
	{
		//This is generated from the path.
		//It needs to be cleared.            
		formatName = null;
		queuePath = null;
		attached = false;
		administerGranted = false;
		browseGranted = false;
		sendGranted = false;
		receiveGranted = false;
		peekGranted = false;

		if (disposing)
		{
			if (mqInfo != null)
			{
				mqInfo.Release();

				//No need to check references in this case, the only object
				//mqInfo is not cached if both conditions are satisified.
				if (sharedMode == NativeMethods.QUEUE_SHARED_MODE_DENY_RECEIVE || !enableCache)
					mqInfo.Dispose();

				mqInfo = null;
			}
		}
	}

	public static MessageQueue Create(string path)
	{
		return Create(path, false);
	}

	public static MessageQueue Create(string path, bool transactional)
	{
		if (path == null)
			throw new ArgumentNullException(nameof(path));

		if (path.Length == 0)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, nameof(path), path));

		if (!IsCanonicalPath(path, true))
			throw new ArgumentException(Res.GetString(Res.InvalidQueuePathToCreate, path));

		MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Administer, MessageQueuePermission.Any);
		permission.Demand();

		//Create properties.
		QueuePropertyVariants properties = new QueuePropertyVariants();
		properties.SetString(NativeMethods.QUEUE_PROPID_PATHNAME, Message.StringToBytes(path));
		if (transactional)
			properties.SetUI1(NativeMethods.QUEUE_PROPID_TRANSACTION, NativeMethods.QUEUE_TRANSACTIONAL_TRANSACTIONAL);
		else
			properties.SetUI1(NativeMethods.QUEUE_PROPID_TRANSACTION, NativeMethods.QUEUE_TRANSACTIONAL_NONE);

		StringBuilder formatName = new StringBuilder(NativeMethods.MAX_LABEL_LEN);
		int formatNameLen = NativeMethods.MAX_LABEL_LEN;

		//Try to create queue.
		int status = UnsafeNativeMethods.MQCreateQueue(IntPtr.Zero, properties.Lock(), formatName, ref formatNameLen);
		properties.Unlock();
		if (IsFatalError(status))
			throw new MessageQueueException(status);

		return new MessageQueue(path);
	}

	public Cursor CreateCursor()
	{
		return new Cursor(this);
	}

	private static MessageQueue[] CreateMessageQueuesSnapshot(MessageQueueCriteria criteria)
	{
		return CreateMessageQueuesSnapshot(criteria, true);
	}

	private static MessageQueue[] CreateMessageQueuesSnapshot(MessageQueueCriteria criteria, bool checkSecurity)
	{
		ArrayList messageQueuesList = new ArrayList();
		MessageQueueEnumerator messageQueues = GetMessageQueueEnumerator(criteria, checkSecurity);
		while (messageQueues.MoveNext())
		{
			MessageQueue messageQueue = messageQueues.Current;
			messageQueuesList.Add(messageQueue);
		}

		MessageQueue[] queues = new MessageQueue[messageQueuesList.Count];
		messageQueuesList.CopyTo(queues, 0);
		return queues;
	}

	public static void Delete(string path)
	{
		if (path == null)
			throw new ArgumentNullException(nameof(path));

		if (path.Length == 0)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "path", path));

		if (!ValidatePath(path, false))
			throw new ArgumentException(Res.GetString(Res.PathSyntax));

		int status = 0;
		MessageQueue queue = new MessageQueue(path);
		MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Administer, PREFIX_FORMAT_NAME + queue.FormatName);
		permission.Demand();

		status = UnsafeNativeMethods.MQDeleteQueue(queue.FormatName);
		if (IsFatalError(status))
			throw new MessageQueueException(status);

		queueInfoCache.Remove(queue.QueueInfoKey);
		formatNameCache.Remove(path.ToUpper(CultureInfo.InvariantCulture));
	}

	[HostProtection(SharedState = true)] // Overriden member of Component. We should not change Component's behavior in the derived class.
	protected override void Dispose(bool disposing)
	{
		Cleanup(disposing);

		base.Dispose(disposing);
	}

	public Message EndPeek(IAsyncResult asyncResult)
	{
		return EndAsyncOperation(asyncResult);
	}

	public Message EndReceive(IAsyncResult asyncResult)
	{
		return EndAsyncOperation(asyncResult);
	}

	private Message EndAsyncOperation(IAsyncResult asyncResult)
	{
		if (asyncResult == null)
			throw new ArgumentNullException(nameof(asyncResult));

		if (!(asyncResult is AsynchronousRequest))
			throw new ArgumentException(Res.GetString(Res.AsyncResultInvalid));

		AsynchronousRequest request = (AsynchronousRequest)asyncResult;

		return request.End();
	}

	public static bool Exists(string path)
	{
		if (path == null)
			throw new ArgumentNullException(nameof(path));

		if (!ValidatePath(path, false))
			throw new ArgumentException(Res.GetString(Res.PathSyntax));

		MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, MessageQueuePermission.Any);
		permission.Demand();

		string pathUpper = path.ToUpper(CultureInfo.InvariantCulture);
		if (pathUpper.StartsWith(PREFIX_FORMAT_NAME))
		{
			throw new InvalidOperationException(Res.GetString(Res.QueueExistsError));
		}
		else if (pathUpper.StartsWith(PREFIX_LABEL))
		{
			MessageQueue labeledQueue = ResolveQueueFromLabel(path, false);
			return labeledQueue != null;
		}
		else
		{
			string formatName = ResolveFormatNameFromQueuePath(path, false);
			return formatName != null;
		}
	}

	private void GenerateQueueProperties()
	{
		if (!browseGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			browseGranted = true;
		}

		int status = UnsafeNativeMethods.MQGetQueueProperties(FormatName, Properties.Lock());
		Properties.Unlock();
		if (IsFatalError(status))
			throw new MessageQueueException(status);
	}

	public Message[] GetAllMessages()
	{
		ArrayList messageList = new ArrayList();
		MessageEnumerator messages = GetMessageEnumerator2();
		while (messages.MoveNext())
		{
			Message message = messages.Current;
			messageList.Add(message);
		}

		Message[] resultMessages = new Message[messageList.Count];
		messageList.CopyTo(resultMessages, 0);
		return resultMessages;
	}

	[Obsolete("This method returns a MessageEnumerator that implements RemoveCurrent family of methods incorrectly. Please use GetMessageEnumerator2 instead.")]
	public IEnumerator GetEnumerator()
	{
		return GetMessageEnumerator();
	}

	public static Guid GetMachineId(string machineName)
	{
		if (!SyntaxCheck.CheckMachineName(machineName))
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "MachineName", machineName));

		if (machineName == ".")
			machineName = ComputerName;

		MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, MessageQueuePermission.Any);
		permission.Demand();

		MachinePropertyVariants machineProperties = new MachinePropertyVariants();
		byte[] bytes = new byte[16];
		machineProperties.SetNull(NativeMethods.MACHINE_ID);
		int status = UnsafeNativeMethods.MQGetMachineProperties(machineName, IntPtr.Zero, machineProperties.Lock());
		machineProperties.Unlock();
		IntPtr handle = machineProperties.GetIntPtr(NativeMethods.MACHINE_ID);
		if (IsFatalError(status))
		{
			if (handle != IntPtr.Zero)
				SafeNativeMethods.MQFreeMemory(handle);

			throw new MessageQueueException(status);
		}

		if (handle != IntPtr.Zero)
		{
			Marshal.Copy(handle, bytes, 0, 16);
			SafeNativeMethods.MQFreeMemory(handle);
		}

		return new Guid(bytes);
	}

	public static SecurityContext GetSecurityContext()
	{
		// SECURITY: Note that this call is not marked with SUCS attribute (i.e., requires FullTrust)   
		int status = NativeMethods.MQGetSecurityContextEx(out var handle);
		if (IsFatalError(status))
			throw new MessageQueueException(status);

		return new SecurityContext(handle);
	}

	public static MessageQueueEnumerator GetMessageQueueEnumerator()
	{
		return new MessageQueueEnumerator(null);
	}

	public static MessageQueueEnumerator GetMessageQueueEnumerator(MessageQueueCriteria criteria)
	{
		return new MessageQueueEnumerator(criteria);
	}

	internal static MessageQueueEnumerator GetMessageQueueEnumerator(MessageQueueCriteria criteria, bool checkSecurity)
	{
		return new MessageQueueEnumerator(criteria, checkSecurity);
	}

	[Obsolete("This method returns a MessageEnumerator that implements RemoveCurrent family of methods incorrectly. Please use GetMessageEnumerator2 instead.")]
	public MessageEnumerator GetMessageEnumerator()
	{
		if (!peekGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Peek, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			peekGranted = true;
		}

		return new MessageEnumerator(this, false);
	}

	public MessageEnumerator GetMessageEnumerator2()
	{
		if (!peekGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Peek, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			peekGranted = true;
		}

		return new MessageEnumerator(this, true);
	}

	public static MessageQueue[] GetPrivateQueuesByMachine(string machineName)
	{
		if (!SyntaxCheck.CheckMachineName(machineName))
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "MachineName", machineName));

		MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, MessageQueuePermission.Any);
		permission.Demand();

		if (machineName == "." || string.Compare(machineName, ComputerName, true, CultureInfo.InvariantCulture) == 0)
			machineName = null;

		MessagePropertyVariants properties = new MessagePropertyVariants(5, 0);
		properties.SetNull(NativeMethods.MANAGEMENT_PRIVATEQ);
		int status = UnsafeNativeMethods.MQMgmtGetInfo(machineName, "MACHINE", properties.Lock());
		properties.Unlock();
		if (IsFatalError(status))
			throw new MessageQueueException(status);

		uint len = properties.GetStringVectorLength(NativeMethods.MANAGEMENT_PRIVATEQ);
		IntPtr basePointer = properties.GetStringVectorBasePointer(NativeMethods.MANAGEMENT_PRIVATEQ);
		MessageQueue[] queues = new MessageQueue[len];
		for (int index = 0; index < len; ++index)
		{
			IntPtr stringPointer = Marshal.ReadIntPtr((IntPtr)((long)basePointer + index * IntPtr.Size));
			//Using Unicode API even on Win9x
			string path = Marshal.PtrToStringUni(stringPointer);
			queues[index] = new MessageQueue("FormatName:DIRECT=OS:" + path) { queuePath = path };

			SafeNativeMethods.MQFreeMemory(stringPointer);
		}

		SafeNativeMethods.MQFreeMemory(basePointer);
		return queues;
	}

	public static MessageQueue[] GetPublicQueues()
	{
		return CreateMessageQueuesSnapshot(null);
	}

	public static MessageQueue[] GetPublicQueues(MessageQueueCriteria criteria)
	{
		return CreateMessageQueuesSnapshot(criteria);
	}

	public static MessageQueue[] GetPublicQueuesByCategory(Guid category)
	{
		MessageQueueCriteria criteria = new MessageQueueCriteria { Category = category };
		return CreateMessageQueuesSnapshot(criteria);
	}

	public static MessageQueue[] GetPublicQueuesByLabel(string label)
	{
		return GetPublicQueuesByLabel(label, true);
	}

	private static MessageQueue[] GetPublicQueuesByLabel(string label, bool checkSecurity)
	{
		MessageQueueCriteria criteria = new MessageQueueCriteria { Label = label };
		return CreateMessageQueuesSnapshot(criteria, checkSecurity);
	}

	public static MessageQueue[] GetPublicQueuesByMachine(string machineName)
	{
		if (!SyntaxCheck.CheckMachineName(machineName))
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "MachineName", machineName));

		MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Browse, MessageQueuePermission.Any);
		permission.Demand();

		try
		{
			//DirectoryServicesPermission dsPermission = new DirectoryServicesPermission(PermissionState.Unrestricted);
			//dsPermission.Assert();

			DirectorySearcher localComputerSearcher = new DirectorySearcher(string.Format(CultureInfo.InvariantCulture, "(&(CN={0})(objectCategory=Computer))", ComputerName));
			SearchResult localComputer = localComputerSearcher.FindOne();
			if (localComputer != null)
			{
				DirectorySearcher localComputerMsmqSearcher =
					new DirectorySearcher(localComputer.GetDirectoryEntry()) { Filter = "(CN=msmq)" };
				SearchResult localMsmqNode = localComputerMsmqSearcher.FindOne();
				SearchResult remoteMsmqNode = null;
				if (localMsmqNode != null)
				{
					if (machineName != "." && string.Compare(machineName, ComputerName, true, CultureInfo.InvariantCulture) != 0)
					{
						DirectorySearcher remoteComputerSearcher = new DirectorySearcher(string.Format(CultureInfo.InvariantCulture, "(&(CN={0})(objectCategory=Computer))", machineName));
						SearchResult remoteComputer = remoteComputerSearcher.FindOne();
						if (remoteComputer == null)
							return Array.Empty<MessageQueue>();

						DirectorySearcher remoteComputerMsmqSearcher =
							new DirectorySearcher(remoteComputer.GetDirectoryEntry()) { Filter = "(CN=msmq)" };
						remoteMsmqNode = remoteComputerMsmqSearcher.FindOne();
						if (remoteMsmqNode == null)
							return Array.Empty<MessageQueue>();
					}
					else
					{
						remoteMsmqNode = localMsmqNode;
					}

					DirectorySearcher objectsSearcher = new DirectorySearcher(remoteMsmqNode.GetDirectoryEntry())
					{
						Filter = "(objectClass=mSMQQueue)"
					};
					objectsSearcher.PropertiesToLoad.Add("Name");
					SearchResultCollection objects = objectsSearcher.FindAll();
					MessageQueue[] queues = new MessageQueue[objects.Count];
					for (int index = 0; index < queues.Length; ++index)
					{
						string queueName = (string)objects[index].Properties["Name"][0];
						queues[index] = new MessageQueue(string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", machineName, queueName));
					}

					return queues;
				}
			}
		}
		catch
		{
			//Ignore all exceptions, so we can gracefully downgrade to use MQ locator.
		}
		//finally
		//{
		//    DirectoryServicesPermission.RevertAssert();
		//}

		MessageQueueCriteria criteria = new MessageQueueCriteria { MachineName = machineName };
		return CreateMessageQueuesSnapshot(criteria, false);
	}

	private static bool IsCanonicalPath(string path, bool checkQueueNameSize)
	{
		if (!ValidatePath(path, checkQueueNameSize))
			return false;

		string upperPath = path.ToUpper(CultureInfo.InvariantCulture);
		return !upperPath.StartsWith(PREFIX_LABEL) &&
			!upperPath.StartsWith(PREFIX_FORMAT_NAME) &&
			!upperPath.EndsWith(SUFIX_DEADLETTER) &&
			!upperPath.EndsWith(SUFIX_DEADXACT) &&
			!upperPath.EndsWith(SUFIX_JOURNAL);
	}

	internal static bool IsFatalError(int value)
	{
		bool isSuccessful = value == 0x00000000;
		bool isInformation = (value & unchecked((int)0xC0000000)) == 0x40000000;
		return !isInformation && !isSuccessful;
	}

	internal static bool IsMemoryError(int value)
	{
		return value == (int)MessageQueueErrorCode.BufferOverflow ||
			 value == (int)MessageQueueErrorCode.LabelBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.ProviderNameBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.SenderCertificateBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.SenderIdBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.SecurityDescriptorBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.SignatureBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.SymmetricKeyBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.UserBufferTooSmall ||
			 value == (int)MessageQueueErrorCode.FormatNameBufferTooSmall;
	}

	private void OnRequestCompleted(IAsyncResult asyncResult)
	{
		if (((AsynchronousRequest)asyncResult).Action == NativeMethods.QUEUE_ACTION_PEEK_CURRENT)
		{
			if (onPeekCompleted != null)
			{
				PeekCompletedEventArgs eventArgs = new PeekCompletedEventArgs(this, asyncResult);
				onPeekCompleted(this, eventArgs);
			}
		}
		else
		{
			if (onReceiveCompleted != null)
			{
				ReceiveCompletedEventArgs eventArgs = new ReceiveCompletedEventArgs(this, asyncResult);
				onReceiveCompleted(this, eventArgs);
			}
		}
	}

	public Message Peek()
	{
		return ReceiveCurrent(InfiniteTimeout, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, CursorHandle.NullHandle, MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
	}

	public Message Peek(TimeSpan timeout)
	{
		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, CursorHandle.NullHandle, MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
	}

	public Message Peek(TimeSpan timeout, Cursor cursor, PeekAction action)
	{
		if (action != PeekAction.Current && action != PeekAction.Next)
			throw new ArgumentOutOfRangeException(Res.GetString(Res.InvalidParameter, "action", action.ToString()));

		if (cursor == null)
			throw new ArgumentNullException(nameof(cursor));

		return ReceiveCurrent(timeout, (int)action, cursor.Handle, MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
	}

	public Message PeekById(string id)
	{
		return ReceiveBy(id, TimeSpan.Zero, false, true, false, null, MessageQueueTransactionType.None);
	}

	public Message PeekById(string id, TimeSpan timeout)
	{
		return ReceiveBy(id, timeout, false, true, true, null, MessageQueueTransactionType.None);
	}

	public Message PeekByCorrelationId(string correlationId)
	{
		return ReceiveBy(correlationId, TimeSpan.Zero, false, false, false, null, MessageQueueTransactionType.None);
	}

	public Message PeekByCorrelationId(string correlationId, TimeSpan timeout)
	{
		return ReceiveBy(correlationId, timeout, false, false, true, null, MessageQueueTransactionType.None);
	}

	public void Purge()
	{
		if (!receiveGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Receive, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			receiveGranted = true;
		}

		int status = StaleSafePurgeQueue();
		if (IsFatalError(status))
			throw new MessageQueueException(status);
	}

	public Message Receive()
	{
		return ReceiveCurrent(InfiniteTimeout, NativeMethods.QUEUE_ACTION_RECEIVE, CursorHandle.NullHandle, MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
	}

	public Message Receive(MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return ReceiveCurrent(InfiniteTimeout, NativeMethods.QUEUE_ACTION_RECEIVE, CursorHandle.NullHandle, MessageReadPropertyFilter, transaction, MessageQueueTransactionType.None);
	}

	public Message Receive(MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return ReceiveCurrent(InfiniteTimeout, NativeMethods.QUEUE_ACTION_RECEIVE, CursorHandle.NullHandle, MessageReadPropertyFilter, null, transactionType);
	}

	public Message Receive(TimeSpan timeout)
	{
		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, CursorHandle.NullHandle, MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
	}

	public Message Receive(TimeSpan timeout, Cursor cursor)
	{
		if (cursor == null)
			throw new ArgumentNullException(nameof(cursor));

		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, cursor.Handle, MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
	}

	public Message Receive(TimeSpan timeout, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, CursorHandle.NullHandle, MessageReadPropertyFilter, transaction, MessageQueueTransactionType.None);
	}

	public Message Receive(TimeSpan timeout, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, CursorHandle.NullHandle, MessageReadPropertyFilter, null, transactionType);
	}

	public Message Receive(TimeSpan timeout, Cursor cursor, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		if (cursor == null)
			throw new ArgumentNullException(nameof(cursor));

		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, cursor.Handle, MessageReadPropertyFilter, transaction, MessageQueueTransactionType.None);
	}

	public Message Receive(TimeSpan timeout, Cursor cursor, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		if (cursor == null)
			throw new ArgumentNullException(nameof(cursor));

		return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, cursor.Handle, MessageReadPropertyFilter, null, transactionType);
	}

	private unsafe IAsyncResult ReceiveAsync(TimeSpan timeout, CursorHandle cursorHandle, int action, AsyncCallback callback, object stateObject)
	{
		long timeoutInMilliseconds = (long)timeout.TotalMilliseconds;
		if (timeoutInMilliseconds < 0 || timeoutInMilliseconds > uint.MaxValue)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "timeout", timeout.ToString()));

		if (action == NativeMethods.QUEUE_ACTION_RECEIVE)
		{
			if (!receiveGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Receive, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				receiveGranted = true;
			}
		}
		else
		{
			if (!peekGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Peek, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				peekGranted = true;
			}
		}

		if (!attached)
		{
			lock (this)
			{
				if (!attached)
				{
					MessageQueueHandle handle = MQInfo.ReadHandle;
					// If GetHandleInformation returns false, it means that the 
					// handle created for reading is not a File handle.
					if (!SafeNativeMethods.GetHandleInformation(handle, out var handleInformation))
					{
						// If not a File handle, need to use MSMQ
						// APC based async IO.
						// We will need to store references to pending async requests (bug 88607)
						useThreadPool = false;
					}
					else
					{
						// File handle can use IOCompletion ports
						// since it only happens for NT
						MQInfo.BindToThreadPool();
						useThreadPool = true;
					}
					attached = true;
				}
			}
		}

		if (callback == null)
		{
			if (onRequestCompleted == null)
				onRequestCompleted = new AsyncCallback(OnRequestCompleted);

			callback = onRequestCompleted;
		}

		AsynchronousRequest request = new AsynchronousRequest(this, (uint)timeoutInMilliseconds, cursorHandle, action, useThreadPool, stateObject, callback);

		//
		// Bug 88607 - keep a reference to outstanding asyncresult so its' not GCed
		//  This applies when GetHandleInformation returns false -> useThreadPool set to false
		//  It should only happen on dependent client, but we here we cover all GetHandleInformation
		//  failure paths for robustness.
		//
		// Need to add reference before calling BeginRead because request can complete by the time 
		// reference is added, and it will be leaked if added to table after completion
		//
		if (!useThreadPool)
		{
			OutstandingAsyncRequests[request] = request;
		}

		request.BeginRead();

		return request;
	}

	private Message ReceiveBy(string id, TimeSpan timeout, bool remove, bool compareId, bool throwTimeout, MessageQueueTransaction transaction, MessageQueueTransactionType transactionType)
	{
		if (id == null)
			throw new ArgumentNullException(nameof(id));

		if (timeout < TimeSpan.Zero || timeout > InfiniteTimeout)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "timeout", timeout.ToString()));

		MessagePropertyFilter oldFilter = receiveFilter;

		CursorHandle cursorHandle = null;
		try
		{
			receiveFilter = new MessagePropertyFilter();
			receiveFilter.ClearAll();
			if (!compareId)
				receiveFilter.CorrelationId = true;
			else
				receiveFilter.Id = true;

			//
			// Use cursor (and not MessageEnumerator) to navigate the queue because enumerator implementation can be incorrect
			// in multithreaded scenarios (see bug 329311)
			//

			//
			// Get cursor handle
			//
			int status = SafeNativeMethods.MQCreateCursor(MQInfo.ReadHandle, out cursorHandle);
			if (IsFatalError(status))
				throw new MessageQueueException(status);

			try
			{
				//
				// peek first message in the queue
				//
				Message message = ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, cursorHandle,
													MessageReadPropertyFilter, null, MessageQueueTransactionType.None);

				while (message != null)
				{
					if (compareId && string.Compare(message.Id, id, true, CultureInfo.InvariantCulture) == 0 ||
						!compareId && string.Compare(message.CorrelationId, id, true, CultureInfo.InvariantCulture) == 0)
					{
						//
						// Found matching message, receive it and return
						//
						receiveFilter = oldFilter;

						if (remove)
						{
							if (transaction == null)
							{
								return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, cursorHandle,
																					  MessageReadPropertyFilter, null, transactionType);
							}
							else
							{
								return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_RECEIVE, cursorHandle,
																					  MessageReadPropertyFilter, transaction, MessageQueueTransactionType.None);
							}
						}
						else
						{
							return ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_PEEK_CURRENT, cursorHandle,
																			  MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
						}
					} //end if

					//
					// Continue search, peek next message
					//
					message = ReceiveCurrent(timeout, NativeMethods.QUEUE_ACTION_PEEK_NEXT, cursorHandle,
													MessageReadPropertyFilter, null, MessageQueueTransactionType.None);
				}
			}
			catch (MessageQueueException)
			{
				// don't do anything, just use this catch as convenient means to exit the search
			}
		}
		finally
		{
			receiveFilter = oldFilter;
			cursorHandle?.Close();
		}

		if (!throwTimeout)
			throw new InvalidOperationException(Res.GetString("MessageNotFound"));
		else
			throw new MessageQueueException((int)MessageQueueErrorCode.IOTimeout);
	}

	public Message ReceiveById(string id)
	{
		return ReceiveBy(id, TimeSpan.Zero, true, true, false, null, MessageQueueTransactionType.None);
	}

	public Message ReceiveById(string id, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return ReceiveBy(id, TimeSpan.Zero, true, true, false, transaction, MessageQueueTransactionType.None);
	}

	public Message ReceiveById(string id, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return ReceiveBy(id, TimeSpan.Zero, true, true, false, null, transactionType);
	}

	public Message ReceiveById(string id, TimeSpan timeout)
	{
		return ReceiveBy(id, timeout, true, true, true, null, MessageQueueTransactionType.None);
	}

	public Message ReceiveById(string id, TimeSpan timeout, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return ReceiveBy(id, timeout, true, true, true, transaction, MessageQueueTransactionType.None);
	}

	public Message ReceiveById(string id, TimeSpan timeout, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return ReceiveBy(id, timeout, true, true, true, null, transactionType);
	}

	public Message ReceiveByCorrelationId(string correlationId)
	{
		return ReceiveBy(correlationId, TimeSpan.Zero, true, false, false, null, MessageQueueTransactionType.None);
	}

	public Message ReceiveByCorrelationId(string correlationId, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return ReceiveBy(correlationId, TimeSpan.Zero, true, false, false, transaction, MessageQueueTransactionType.None);
	}

	public Message ReceiveByCorrelationId(string correlationId, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return ReceiveBy(correlationId, TimeSpan.Zero, true, false, false, null, transactionType);
	}

	public Message ReceiveByCorrelationId(string correlationId, TimeSpan timeout)
	{
		return ReceiveBy(correlationId, timeout, true, false, true, null, MessageQueueTransactionType.None);
	}

	public Message ReceiveByCorrelationId(string correlationId, TimeSpan timeout, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		return ReceiveBy(correlationId, timeout, true, false, true, transaction, MessageQueueTransactionType.None);
	}

	public Message ReceiveByCorrelationId(string correlationId, TimeSpan timeout, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		return ReceiveBy(correlationId, timeout, true, false, true, null, transactionType);
	}

	public Message ReceiveByLookupId(long lookupId)
	{
		return InternalReceiveByLookupId(true, MessageLookupAction.Current, lookupId, null, MessageQueueTransactionType.None);
	}

	public Message ReceiveByLookupId(MessageLookupAction action, long lookupId, MessageQueueTransactionType transactionType)
	{
		return InternalReceiveByLookupId(true, action, lookupId, null, transactionType);
	}

	public Message ReceiveByLookupId(MessageLookupAction action, long lookupId, MessageQueueTransaction transaction)
	{
		return InternalReceiveByLookupId(true, action, lookupId, transaction, MessageQueueTransactionType.None);
	}

	public Message PeekByLookupId(long lookupId)
	{
		return InternalReceiveByLookupId(false, MessageLookupAction.Current, lookupId, null, MessageQueueTransactionType.None);
	}

	public Message PeekByLookupId(MessageLookupAction action, long lookupId)
	{
		return InternalReceiveByLookupId(false, action, lookupId, null, MessageQueueTransactionType.None);
	}

	internal unsafe Message InternalReceiveByLookupId(bool receive, MessageLookupAction lookupAction, long lookupId,
		MessageQueueTransaction internalTransaction, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		if (!ValidationUtility.ValidateMessageLookupAction(lookupAction))
			throw new InvalidEnumArgumentException("action", (int)lookupAction, typeof(MessageLookupAction));

		if (!Msmq3OrNewer)
			throw new PlatformNotSupportedException(Res.GetString(Res.PlatformNotSupported));

		int action;

		if (receive)
		{
			if (!receiveGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Receive, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				receiveGranted = true;
			}

			action = NativeMethods.LOOKUP_RECEIVE_MASK | (int)lookupAction;
		}
		else
		{
			if (!peekGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Peek, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				peekGranted = true;
			}

			action = NativeMethods.LOOKUP_PEEK_MASK | (int)lookupAction;
		}

		MessagePropertyFilter filter = MessageReadPropertyFilter;

		int status = 0;
		Message receiveMessage = null;
		MessagePropertyVariants.MQPROPS lockedReceiveMessage = null;
		if (filter != null)
		{
			receiveMessage = new Message((MessagePropertyFilter)filter.Clone());
			receiveMessage.SetLookupId(lookupId);

			if (formatter != null)
				receiveMessage.Formatter = (IMessageFormatter)formatter.Clone();

			lockedReceiveMessage = receiveMessage.Lock();
		}

		try
		{
			if (internalTransaction != null && receive)
				status = StaleSafeReceiveByLookupId(lookupId, action, lockedReceiveMessage, null, null, internalTransaction.BeginQueueOperation());
			else
				status = StaleSafeReceiveByLookupId(lookupId, action, lockedReceiveMessage, null, null, (IntPtr)transactionType);

			if (receiveMessage != null)
			{
				//Need to keep trying until enough space has been allocated.
				//Concurrent scenarions might not succeed on the second retry.
				while (IsMemoryError(status))
				{
					receiveMessage.Unlock();
					receiveMessage.AdjustMemory();
					lockedReceiveMessage = receiveMessage.Lock();
					if (internalTransaction != null && receive)
						status = StaleSafeReceiveByLookupId(lookupId, action, lockedReceiveMessage, null, null, internalTransaction.InnerTransaction);
					else
						status = StaleSafeReceiveByLookupId(lookupId, action, lockedReceiveMessage, null, null, (IntPtr)transactionType);
				}

				receiveMessage.Unlock();
			}
		}
		finally
		{
			if (internalTransaction != null && receive)
				internalTransaction.EndQueueOperation();
		}

		if (status == (int)MessageQueueErrorCode.MessageNotFound)
			throw new InvalidOperationException(Res.GetString("MessageNotFound"));

		if (IsFatalError(status))
			throw new MessageQueueException(status);

		return receiveMessage;
	}

	internal unsafe Message ReceiveCurrent(TimeSpan timeout, int action, CursorHandle cursor, MessagePropertyFilter filter, MessageQueueTransaction internalTransaction, MessageQueueTransactionType transactionType)
	{
		long timeoutInMilliseconds = (long)timeout.TotalMilliseconds;
		if (timeoutInMilliseconds < 0 || timeoutInMilliseconds > uint.MaxValue)
			throw new ArgumentException(Res.GetString(Res.InvalidParameter, "timeout", timeout.ToString()));

		if (action == NativeMethods.QUEUE_ACTION_RECEIVE)
		{
			if (!receiveGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Receive, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				receiveGranted = true;
			}
		}
		else
		{
			if (!peekGranted)
			{
				MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Peek, PREFIX_FORMAT_NAME + FormatName);
				permission.Demand();

				peekGranted = true;
			}
		}

		int status = 0;
		Message receiveMessage = null;
		MessagePropertyVariants.MQPROPS lockedReceiveMessage = null;
		if (filter != null)
		{
			receiveMessage = new Message((MessagePropertyFilter)filter.Clone());
			if (formatter != null)
				receiveMessage.Formatter = (IMessageFormatter)formatter.Clone();

			lockedReceiveMessage = receiveMessage.Lock();
		}

		try
		{
			if (internalTransaction != null)
				status = StaleSafeReceiveMessage((uint)timeoutInMilliseconds, action, lockedReceiveMessage, null, null, cursor, internalTransaction.BeginQueueOperation());
			else
				status = StaleSafeReceiveMessage((uint)timeoutInMilliseconds, action, lockedReceiveMessage, null, null, cursor, (IntPtr)transactionType);

			if (receiveMessage != null)
			{
				//Need to keep trying until enough space has been allocated.
				//Concurrent scenarions might not succeed on the second retry.
				while (IsMemoryError(status))
				{
					// Need to special-case retrying PeekNext after a buffer overflow 
					// by using PeekCurrent on retries since otherwise MSMQ will
					// advance the cursor, skipping messages
					if (action == NativeMethods.QUEUE_ACTION_PEEK_NEXT)
						action = NativeMethods.QUEUE_ACTION_PEEK_CURRENT;
					receiveMessage.Unlock();
					receiveMessage.AdjustMemory();
					lockedReceiveMessage = receiveMessage.Lock();
					if (internalTransaction != null)
						status = StaleSafeReceiveMessage((uint)timeoutInMilliseconds, action, lockedReceiveMessage, null, null, cursor, internalTransaction.InnerTransaction);
					else
						status = StaleSafeReceiveMessage((uint)timeoutInMilliseconds, action, lockedReceiveMessage, null, null, cursor, (IntPtr)transactionType);
				}
			}
		}
		finally
		{
			receiveMessage?.Unlock();

			internalTransaction?.EndQueueOperation();
		}

		if (IsFatalError(status))
			throw new MessageQueueException(status);

		return receiveMessage;
	}

	public void Refresh()
	{
		PropertyFilter.ClearAll();
	}

	private void SaveQueueProperties()
	{
		if (!administerGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Administer, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			administerGranted = true;
		}

		int status = UnsafeNativeMethods.MQSetQueueProperties(FormatName, Properties.Lock());
		Properties.Unlock();
		if (IsFatalError(status))
			throw new MessageQueueException(status);
	}

	public void Send(object obj)
	{
		SendInternal(obj, null, MessageQueueTransactionType.None);
	}

	public void Send(object obj, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		SendInternal(obj, transaction, MessageQueueTransactionType.None);
	}

	public void Send(object obj, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		SendInternal(obj, null, transactionType);
	}

	public void Send(object obj, string label)
	{
		Send(obj, label, null, MessageQueueTransactionType.None);
	}

	public void Send(object obj, string label, MessageQueueTransaction transaction)
	{
		if (transaction == null)
			throw new ArgumentNullException(nameof(transaction));

		Send(obj, label, transaction, MessageQueueTransactionType.None);
	}

	public void Send(object obj, string label, MessageQueueTransactionType transactionType)
	{
		if (!ValidationUtility.ValidateMessageQueueTransactionType(transactionType))
			throw new InvalidEnumArgumentException("transactionType", (int)transactionType, typeof(MessageQueueTransactionType));

		Send(obj, label, null, transactionType);
	}

	private void Send(object obj, string label, MessageQueueTransaction transaction, MessageQueueTransactionType transactionType)
	{
		if (label == null)
			throw new ArgumentNullException(nameof(label));

		if (obj is Message message)
		{
			message.Label = label;
			SendInternal(message, transaction, transactionType);
		}
		else
		{
			string oldLabel = DefaultPropertiesToSend.Label;
			try
			{
				DefaultPropertiesToSend.Label = label;
				SendInternal(obj, transaction, transactionType);
			}
			finally
			{
				DefaultPropertiesToSend.Label = oldLabel;
			}
		}
	}

	private void SendInternal(object obj, MessageQueueTransaction internalTransaction, MessageQueueTransactionType transactionType)
	{
		if (!sendGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Send, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			sendGranted = true;
		}

		Message message = null;
		if (obj is Message message1)
			message = message1;

		if (message == null)
		{
			message = DefaultPropertiesToSend.CachedMessage;
			message.Formatter = Formatter;
			message.Body = obj;
		}

		//Write cached properties and if message is being forwarded Clear queue specific properties            
		int status = 0;
		message.AdjustToSend();
		MessagePropertyVariants.MQPROPS properties = message.Lock();
		try
		{
			if (internalTransaction != null)
				status = StaleSafeSendMessage(properties, internalTransaction.BeginQueueOperation());
			else
				status = StaleSafeSendMessage(properties, (IntPtr)transactionType);
		}
		finally
		{
			message.Unlock();

			internalTransaction?.EndQueueOperation();
		}

		if (IsFatalError(status))
			throw new MessageQueueException(status);
	}

	private static MessageQueue ResolveQueueFromLabel(string path, bool throwException)
	{
		MessageQueue[] queues = GetPublicQueuesByLabel(path.Substring(PREFIX_LABEL.Length), false);
		if (queues.Length == 0)
		{
			if (throwException)
				throw new InvalidOperationException(Res.GetString(Res.InvalidLabel, path.Substring(PREFIX_LABEL.Length)));

			return null;
		}
		else if (queues.Length > 1)
		{
			throw new InvalidOperationException(Res.GetString(Res.AmbiguousLabel, path.Substring(PREFIX_LABEL.Length)));
		}

		return queues[0];
	}

	private static string ResolveFormatNameFromQueuePath(string queuePath, bool throwException)
	{
		string machine = queuePath.Substring(0, queuePath.IndexOf('\\'));
		//The name includes the \\
		string name = queuePath.Substring(queuePath.IndexOf('\\'));
		//Check for machine DeadLetter or Journal
		if (string.Compare(name, SUFIX_DEADLETTER, true, CultureInfo.InvariantCulture) == 0 ||
			string.Compare(name, SUFIX_DEADXACT, true, CultureInfo.InvariantCulture) == 0 ||
			string.Compare(name, SUFIX_JOURNAL, true, CultureInfo.InvariantCulture) == 0)
		{
			//Need to get the machine Id to construct the format name.

			if (machine.CompareTo(".") == 0)
				machine = ComputerName;

			//Create a guid to get the right format.
			Guid machineId = GetMachineId(machine);
			StringBuilder newFormatName = new StringBuilder();
			//System format names:
			//MACHINE=guid;DEADXACT
			//MACHINE=guid;DEADLETTER
			//MACHINE=guid;JOURNAL
			newFormatName.Append("MACHINE=");
			newFormatName.Append(machineId.ToString());
			if (string.Compare(name, SUFIX_DEADXACT, true, CultureInfo.InvariantCulture) == 0)
				newFormatName.Append(";DEADXACT");
			else if (string.Compare(name, SUFIX_DEADLETTER, true, CultureInfo.InvariantCulture) == 0)
				newFormatName.Append(";DEADLETTER");
			else
				newFormatName.Append(";JOURNAL");

			return newFormatName.ToString();
		}
		else
		{
			string realPath = queuePath;
			bool journal = false;
			if (queuePath.ToUpper(CultureInfo.InvariantCulture).EndsWith(SUFIX_JOURNAL))
			{
				journal = true;
				int lastIndex = realPath.LastIndexOf('\\');
				realPath = realPath.Substring(0, lastIndex);
			}

			int result;
			int status = 0;
			StringBuilder newFormatName = new StringBuilder(NativeMethods.MAX_LABEL_LEN);
			result = NativeMethods.MAX_LABEL_LEN;
			status = SafeNativeMethods.MQPathNameToFormatName(realPath, newFormatName, ref result);
			if (status != 0)
			{
				if (throwException)
					throw new MessageQueueException(status);
				else if (status == (int)MessageQueueErrorCode.IllegalQueuePathName)
					throw new MessageQueueException(status);

				return null;
			}

			if (journal)
				newFormatName.Append(";JOURNAL");

			return newFormatName.ToString();
		}
	}

	public void ResetPermissions()
	{
		if (!administerGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Administer, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			administerGranted = true;
		}

		int result = UnsafeNativeMethods.MQSetQueueSecurity(FormatName, NativeMethods.DACL_SECURITY_INFORMATION, null);
		if (result != NativeMethods.MQ_OK)
			throw new MessageQueueException(result);
	}

	public void SetPermissions(string user, MessageQueueAccessRights rights)
	{
		if (user == null)
			throw new ArgumentNullException(nameof(user));

		SetPermissions(user, rights, AccessControlEntryType.Allow);
	}

	public void SetPermissions(string user, MessageQueueAccessRights rights, AccessControlEntryType entryType)
	{
		if (user == null)
			throw new ArgumentNullException(nameof(user));

		Trustee t = new Trustee(user);
		MessageQueueAccessControlEntry ace = new MessageQueueAccessControlEntry(t, rights, entryType);
		AccessControlList dacl = new AccessControlList { ace };
		SetPermissions(dacl);
	}

	public void SetPermissions(MessageQueueAccessControlEntry ace)
	{
		if (ace == null)
			throw new ArgumentNullException(nameof(ace));

		AccessControlList dacl = new AccessControlList { ace };
		SetPermissions(dacl);
	}

	public void SetPermissions(AccessControlList dacl)
	{
		if (dacl == null)
			throw new ArgumentNullException(nameof(dacl));

		if (!administerGranted)
		{
			MessageQueuePermission permission = new MessageQueuePermission(MessageQueuePermissionAccess.Administer, PREFIX_FORMAT_NAME + FormatName);
			permission.Demand();

			administerGranted = true;
		}

		//Access control is not supported in Win9x, need to check
		//the environment and take appropriate action.
		AccessControlList.CheckEnvironment();

		byte[] SecurityDescriptor = new byte[100];
		int lengthNeeded = 0;
		int mqResult;

		GCHandle sdHandle = GCHandle.Alloc(SecurityDescriptor, GCHandleType.Pinned);
		try
		{
			mqResult = UnsafeNativeMethods.MQGetQueueSecurity(FormatName,
														 NativeMethods.DACL_SECURITY_INFORMATION,
														 sdHandle.AddrOfPinnedObject(),
														 SecurityDescriptor.Length,
														 out lengthNeeded);

			if (mqResult == NativeMethods.MQ_ERROR_SECURITY_DESCRIPTOR_TOO_SMALL)
			{
				sdHandle.Free();
				SecurityDescriptor = new byte[lengthNeeded];
				sdHandle = GCHandle.Alloc(SecurityDescriptor, GCHandleType.Pinned);
				mqResult = UnsafeNativeMethods.MQGetQueueSecurity(FormatName,
															 NativeMethods.DACL_SECURITY_INFORMATION,
															 sdHandle.AddrOfPinnedObject(),
															 SecurityDescriptor.Length,
															 out lengthNeeded);
			}

			if (mqResult != NativeMethods.MQ_OK)
			{
				throw new MessageQueueException(mqResult);
			}

			bool success = UnsafeNativeMethods.GetSecurityDescriptorDacl(sdHandle.AddrOfPinnedObject(),
																			out var daclPresent,
																			out var pDacl,
																			out var daclDefaulted);

			if (!success)
				throw new Win32Exception();

			// At this point we have the DACL for the queue.  Now we need to create
			// a new security descriptor with an updated DACL.

			NativeMethods.SECURITY_DESCRIPTOR newSecurityDescriptor = new NativeMethods.SECURITY_DESCRIPTOR();
			UnsafeNativeMethods.InitializeSecurityDescriptor(newSecurityDescriptor,
																NativeMethods.SECURITY_DESCRIPTOR_REVISION);
			IntPtr newDacl = dacl.MakeAcl(pDacl);
			try
			{
				success = UnsafeNativeMethods.SetSecurityDescriptorDacl(newSecurityDescriptor,
																		   true,
																		   newDacl,
																		   false);

				if (!success)
					throw new Win32Exception();

				int result = UnsafeNativeMethods.MQSetQueueSecurity(FormatName,
															   NativeMethods.DACL_SECURITY_INFORMATION,
															   newSecurityDescriptor);

				if (result != NativeMethods.MQ_OK)
					throw new MessageQueueException(result);
			}
			finally
			{
				AccessControlList.FreeAcl(newDacl);
			}

			//If the format name has been cached, let's
			//remove it, since the process might no longer
			//have access to the corresponding queue.                                
			queueInfoCache.Remove(QueueInfoKey);
			formatNameCache.Remove(path.ToUpper(CultureInfo.InvariantCulture));
		}
		finally
		{
			if (sdHandle.IsAllocated)
				sdHandle.Free();
		}
	}

	internal static bool ValidatePath(string path, bool checkQueueNameSize)
	{
		if (string.IsNullOrEmpty(path))
			return true;

		string upperPath = path.ToUpper(CultureInfo.InvariantCulture);
		if (upperPath.StartsWith(PREFIX_LABEL))
			return true;

		if (upperPath.StartsWith(PREFIX_FORMAT_NAME))
			return true;

		int number = 0;
		int index = -1;
		while (true)
		{
			int newIndex = upperPath.IndexOf('\\', index + 1);
			if (newIndex == -1)
				break;
			else
				index = newIndex;

			++number;
		}

		if (number == 1)
		{
			if (checkQueueNameSize)
			{
				long length = path.Length - (index + 1);
				if (length > 255)
					throw new ArgumentException(Res.GetString(Res.LongQueueName));
			}
			return true;
		}

		if (number == 2)
		{
			if (upperPath.EndsWith(SUFIX_JOURNAL))
				return true;

			index = upperPath.LastIndexOf(SUFIX_PRIVATE + "\\");
			if (index != -1)
				return true;
		}

		if (number == 3 && upperPath.EndsWith(SUFIX_JOURNAL))
		{
			index = upperPath.LastIndexOf(SUFIX_PRIVATE + "\\");
			if (index != -1)
				return true;
		}

		return false;
	}

	internal void SetAccessMode(QueueAccessMode accessMode)
	{
		//
		// this method should only be called from a constructor.
		// we dont support changing queue access mode after contruction time.
		//
		if (!ValidationUtility.ValidateQueueAccessMode(accessMode))
			throw new InvalidEnumArgumentException("accessMode", (int)accessMode, typeof(QueueAccessMode));

		AccessMode = accessMode;
	}

	private class QueuePropertyFilter
	{
		public bool Authenticate;
		public bool BasePriority;
		public bool CreateTime;
		public bool EncryptionLevel;
		public bool Id;
		// disable csharp compiler warning #0414: field assigned unused value
#pragma warning disable 0414
		public bool Transactional;
#pragma warning restore 0414
		public bool Label;
		public bool LastModifyTime;
		public bool MaximumJournalSize;
		public bool MaximumQueueSize;
		public bool MulticastAddress;
		// disable csharp compiler warning #0414: field assigned unused value
#pragma warning disable 0414
		public bool Path;
#pragma warning restore 0414
		public bool Category;
		public bool UseJournalQueue;

		public void ClearAll()
		{
			Authenticate = false;
			BasePriority = false;
			CreateTime = false;
			EncryptionLevel = false;
			Id = false;
			Transactional = false;
			Label = false;
			LastModifyTime = false;
			MaximumJournalSize = false;
			MaximumQueueSize = false;
			Path = false;
			Category = false;
			UseJournalQueue = false;
			MulticastAddress = false;
		}
	}

	private class AsynchronousRequest : IAsyncResult
	{
		private readonly IOCompletionCallback onCompletionStatusChanged;
		private readonly SafeNativeMethods.ReceiveCallback onMessageReceived;
		private readonly AsyncCallback callback;
		private readonly ManualResetEvent resetEvent;
		private readonly MessageQueue owner;
		private int status = 0;
		private Message message;
		private readonly uint timeout;
		private readonly CursorHandle cursorHandle;

		internal unsafe AsynchronousRequest(MessageQueue owner, uint timeout, CursorHandle cursorHandle, int action, bool useThreadPool, object asyncState, AsyncCallback callback)
		{
			this.owner = owner;
			AsyncState = asyncState;
			this.callback = callback;
			Action = action;
			this.timeout = timeout;
			resetEvent = new ManualResetEvent(false);
			this.cursorHandle = cursorHandle;

			if (!useThreadPool)
				onMessageReceived = new SafeNativeMethods.ReceiveCallback(OnMessageReceived);
			else
				onCompletionStatusChanged = new IOCompletionCallback(OnCompletionStatusChanged);
		}

		internal int Action { get; private set; }

		public object AsyncState { get; }

		public WaitHandle AsyncWaitHandle => resetEvent;

		public bool CompletedSynchronously => false;

		public bool IsCompleted { get; private set; }

		internal unsafe void BeginRead()
		{
			NativeOverlapped* overlappedPointer = null;
			if (onCompletionStatusChanged != null)
			{
				Overlapped overlapped = new Overlapped { AsyncResult = this };
				overlappedPointer = overlapped.Pack(onCompletionStatusChanged, null);
			}

			int localStatus = 0;
			message = new Message(owner.MessageReadPropertyFilter);

			try
			{
				localStatus = owner.StaleSafeReceiveMessage(timeout, Action, message.Lock(), overlappedPointer, onMessageReceived, cursorHandle, IntPtr.Zero);
				while (IsMemoryError(localStatus))
				{
					// Need to special-case retrying PeekNext after a buffer overflow 
					// by using PeekCurrent on retries since otherwise MSMQ will
					// advance the cursor, skipping messages
					if (Action == NativeMethods.QUEUE_ACTION_PEEK_NEXT)
						Action = NativeMethods.QUEUE_ACTION_PEEK_CURRENT;
					message.Unlock();
					message.AdjustMemory();
					localStatus = owner.StaleSafeReceiveMessage(timeout, Action, message.Lock(), overlappedPointer, onMessageReceived, cursorHandle, IntPtr.Zero);
				}
			}
			catch (Exception)
			{
				// Here will would do all the cleanup that RaiseCompletionEvent does on failure path,
				// but without raising completion event.
				// This is to preserve pre-Whidbey Beta 2 behavior, when exception thrown from this method 
				// would prevent RaiseCompletionEvent from being called (and also leak resources)
				message.Unlock();

				if (overlappedPointer != null)
					Overlapped.Free(overlappedPointer);

				if (!owner.useThreadPool)
					owner.OutstandingAsyncRequests.Remove(this);

				throw;
			}

			// NOTE: RaiseCompletionEvent is not in a finally block by design, for two reasons:
			// 1) the contract of BeginRead is to throw exception and not to notify event handler.
			// 2) we dont know what the value pf localStatus will be in case of exception
			if (IsFatalError(localStatus))
				RaiseCompletionEvent(localStatus, overlappedPointer);
		}

		internal Message End()
		{
			resetEvent.WaitOne();
			if (IsFatalError(status))
				throw new MessageQueueException(status);

			if (owner.formatter != null)
				message.Formatter = (IMessageFormatter)owner.formatter.Clone();

			return message;
		}

		private unsafe void OnCompletionStatusChanged(uint errorCode, uint numBytes, NativeOverlapped* overlappedPointer)
		{
			int result = 0;
			if (errorCode != 0)
			{
				// MSMQ does a hacky trick to return the operation 
				// result through the completion port.

				// eugenesh Dec 2004. Bug 419155: 
				// NativeOverlapped.InternalLow returns IntPtr, which is 64 bits on a 64 bit platform.
				// It contains MSMQ error code, which, when set to an error value, is outside of the int range
				// Therefore, OverflowException is thrown in checked context. 
				// However, IntPtr (int) operator ALWAYS runs in checked context on 64 bit platforms.
				// Therefore, we first cast to long to avoid OverflowException, and then cast to int
				// in unchecked context 
				long msmqError = (long)overlappedPointer->InternalLow;
				unchecked
				{
					result = (int)msmqError;
				}
			}

			RaiseCompletionEvent(result, overlappedPointer);
		}

		private unsafe void OnMessageReceived(int result, IntPtr handle, int timeout, int action, IntPtr propertiesPointer, NativeOverlapped* overlappedPointer, IntPtr cursorHandle)
		{
			RaiseCompletionEvent(result, overlappedPointer);
		}

		// See comment explaining this SuppressMessage below
		private unsafe void RaiseCompletionEvent(int result, NativeOverlapped* overlappedPointer)
		{
			if (IsMemoryError(result))
			{
				while (IsMemoryError(result))
				{
					// Need to special-case retrying PeekNext after a buffer overflow 
					// by using PeekCurrent on retries since otherwise MSMQ will
					// advance the cursor, skipping messages
					if (Action == NativeMethods.QUEUE_ACTION_PEEK_NEXT)
						Action = NativeMethods.QUEUE_ACTION_PEEK_CURRENT;
					message.Unlock();
					message.AdjustMemory();
					try
					{
						// ReadHandle called from StaleSafeReceiveMessage can throw if the handle has been invalidated
						// (for example, by closing it), and subsequent MQOpenQueue fails for some reason. 
						// Therefore catch exception (otherwise process will die) and propagate error
						// EugeneSh Jan 2006 (Whidbey bug 570055)
						result = owner.StaleSafeReceiveMessage(timeout, Action, message.Lock(), overlappedPointer, onMessageReceived, cursorHandle, IntPtr.Zero);
					}
					catch (MessageQueueException e)
					{
						result = (int)e.MessageQueueErrorCode;
						break;
					}
				}

				if (!IsFatalError(result))
					return;
			}

			message.Unlock();

			if (owner.IsCashedInfoInvalidOnReceive(result))
			{
				owner.MQInfo.Close();
				try
				{
					// For explanation of this try/catch, see comment above
					result = owner.StaleSafeReceiveMessage(timeout, Action, message.Lock(), overlappedPointer, onMessageReceived, cursorHandle, IntPtr.Zero);
				}
				catch (MessageQueueException e)
				{
					result = (int)e.MessageQueueErrorCode;
				}

				if (!IsFatalError(result))
					return;
			}

			status = result;
			if (overlappedPointer != null)
				Overlapped.Free(overlappedPointer);

			IsCompleted = true;
			resetEvent.Set();

#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception
			try
			{
				//
				// 511878: The code below breaks the contract of ISynchronizeInvoke.
				// We fixed it in 367076, but that fix resulted in a regression that is bug 511878.
				// "Proper fix" for 511878 requires special-casing Form. That causes us to 
				// load System.Windows.Forms and System.Drawing, 
				// which were previously not loaded on this path. 
				// As only one customer complained about 367076, we decided to revert to 
				// Everett behavior
				//
				if (owner.SynchronizingObject?.InvokeRequired == true)
				{
					owner.SynchronizingObject.BeginInvoke(callback, new object[] { this });
				}
				else
				{
					callback(this);
				}
			}
			catch (Exception)
			{
				// eugenesh, Dec 2004: Swallowing exceptions here is a serious bug.
				// However, it would be a breaking change to remove this catch, 
				// therefore we decided to preserve the existing behavior 

			}
			finally
			{
				if (!owner.useThreadPool)
				{
					Debug.Assert(owner.OutstandingAsyncRequests.Contains(this));
					owner.OutstandingAsyncRequests.Remove(this);
				}
			}
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception
		}
	}

	private int StaleSafePurgeQueue()
	{
		int status = UnsafeNativeMethods.MQPurgeQueue(MQInfo.ReadHandle);
		if (status == (int)MessageQueueErrorCode.StaleHandle || status == (int)MessageQueueErrorCode.QueueDeleted)
		{
			MQInfo.Close();
			status = UnsafeNativeMethods.MQPurgeQueue(MQInfo.ReadHandle);
		}
		return status;
	}

	private int StaleSafeSendMessage(MessagePropertyVariants.MQPROPS properties, IntPtr transaction)
	{
		//
		// TransactionType.Automatic uses current System.Transactions transaction, if one is available;
		// otherwise, it passes Automatic to MSMQ to support COM+ transactions
		// NOTE: Need careful qualification of class names, 
		// since ITransaction is defined by System.Messaging.Interop, System.Transactions and System.EnterpriseServices
		//
		if ((MessageQueueTransactionType)transaction == MessageQueueTransactionType.Automatic)
		{
			Transaction tx = Transaction.Current;
			if (tx != null)
			{
				IDtcTransaction ntx =
					TransactionInterop.GetDtcTransaction(tx);

				return StaleSafeSendMessage(properties, (ITransaction)ntx);
			}
		}

		int status = UnsafeNativeMethods.MQSendMessage(MQInfo.WriteHandle, properties, transaction);
		if (status == (int)MessageQueueErrorCode.StaleHandle || status == (int)MessageQueueErrorCode.QueueDeleted)
		{
			MQInfo.Close();
			status = UnsafeNativeMethods.MQSendMessage(MQInfo.WriteHandle, properties, transaction);
		}

		return status;
	}

	private int StaleSafeSendMessage(MessagePropertyVariants.MQPROPS properties, ITransaction transaction)
	{
		int status = UnsafeNativeMethods.MQSendMessage(MQInfo.WriteHandle, properties, transaction);
		if (status == (int)MessageQueueErrorCode.StaleHandle || status == (int)MessageQueueErrorCode.QueueDeleted)
		{
			MQInfo.Close();
			status = UnsafeNativeMethods.MQSendMessage(MQInfo.WriteHandle, properties, transaction);
		}
		return status;
	}

	internal unsafe int StaleSafeReceiveMessage(uint timeout, int action, MessagePropertyVariants.MQPROPS properties, NativeOverlapped* overlapped,
																					   SafeNativeMethods.ReceiveCallback receiveCallback, CursorHandle cursorHandle, IntPtr transaction)
	{
		//
		// TransactionType.Automatic uses current System.Transactions transaction, if one is available;
		// otherwise, it passes Automatic to MSMQ to support COM+ transactions
		// NOTE: Need careful qualification of class names, 
		// since ITransaction is defined by System.Messaging.Interop, System.Transactions and System.EnterpriseServices
		//
		if ((MessageQueueTransactionType)transaction == MessageQueueTransactionType.Automatic)
		{
			Transaction tx = Transaction.Current;
			if (tx != null)
			{
				IDtcTransaction ntx =
					TransactionInterop.GetDtcTransaction(tx);

				return StaleSafeReceiveMessage(timeout, action, properties, overlapped, receiveCallback, cursorHandle, (ITransaction)ntx);
			}
		}

		int status = UnsafeNativeMethods.MQReceiveMessage(MQInfo.ReadHandle, timeout, action, properties, overlapped, receiveCallback, cursorHandle, transaction);
		if (IsCashedInfoInvalidOnReceive(status))
		{
			MQInfo.Close(); //invalidate cached ReadHandle, so it will be refreshed on next access
			status = UnsafeNativeMethods.MQReceiveMessage(MQInfo.ReadHandle, timeout, action, properties, overlapped, receiveCallback, cursorHandle, transaction);
		}
		return status;
	}

	private unsafe int StaleSafeReceiveMessage(uint timeout, int action, MessagePropertyVariants.MQPROPS properties, NativeOverlapped* overlapped,
																					   SafeNativeMethods.ReceiveCallback receiveCallback, CursorHandle cursorHandle, ITransaction transaction)
	{
		int status = UnsafeNativeMethods.MQReceiveMessage(MQInfo.ReadHandle, timeout, action, properties, overlapped, receiveCallback, cursorHandle, transaction);
		if (IsCashedInfoInvalidOnReceive(status))
		{
			MQInfo.Close(); //invalidate cached ReadHandle, so it will be refreshed on next access
			status = UnsafeNativeMethods.MQReceiveMessage(MQInfo.ReadHandle, timeout, action, properties, overlapped, receiveCallback, cursorHandle, transaction);
		}
		return status;
	}

	private unsafe int StaleSafeReceiveByLookupId(long lookupId, int action, MessagePropertyVariants.MQPROPS properties,
		NativeOverlapped* overlapped, SafeNativeMethods.ReceiveCallback receiveCallback, IntPtr transaction)
	{
		if ((MessageQueueTransactionType)transaction == MessageQueueTransactionType.Automatic)
		{
			Transaction tx = Transaction.Current;
			if (tx != null)
			{
				IDtcTransaction ntx =
					TransactionInterop.GetDtcTransaction(tx);

				return StaleSafeReceiveByLookupId(lookupId, action, properties, overlapped, receiveCallback, (ITransaction)ntx);
			}
		}

		int status = UnsafeNativeMethods.MQReceiveMessageByLookupId(MQInfo.ReadHandle, lookupId, action, properties, overlapped, receiveCallback, transaction);
		if (IsCashedInfoInvalidOnReceive(status))
		{
			MQInfo.Close(); //invalidate cached ReadHandle, so it will be refreshed on next access
			status = UnsafeNativeMethods.MQReceiveMessageByLookupId(MQInfo.ReadHandle, lookupId, action, properties, overlapped, receiveCallback, transaction);
		}
		return status;
	}

	private unsafe int StaleSafeReceiveByLookupId(long lookupId, int action, MessagePropertyVariants.MQPROPS properties,
		NativeOverlapped* overlapped, SafeNativeMethods.ReceiveCallback receiveCallback, ITransaction transaction)
	{
		int status = UnsafeNativeMethods.MQReceiveMessageByLookupId(MQInfo.ReadHandle, lookupId, action, properties, overlapped, receiveCallback, transaction);
		if (IsCashedInfoInvalidOnReceive(status))
		{
			MQInfo.Close(); //invalidate cached ReadHandle, so it will be refreshed on next access
			status = UnsafeNativeMethods.MQReceiveMessageByLookupId(MQInfo.ReadHandle, lookupId, action, properties, overlapped, receiveCallback, transaction);
		}
		return status;
	}

	private bool IsCashedInfoInvalidOnReceive(int receiveResult)
	{
		// returns true if return code of ReceiveMessage indicates
		// that cached handle used for receive has become invalid 
		return receiveResult == (int)MessageQueueErrorCode.StaleHandle ||      //both qm and ac restarted
				receiveResult == (int)MessageQueueErrorCode.InvalidHandle ||    //get this if ac is not restarted 
				receiveResult == (int)MessageQueueErrorCode.InvalidParameter; // get this on w2k
	}

	internal class CacheTable<Key, Value>
	{
		private Dictionary<Key, CacheEntry<Value>> table;
		private readonly ReaderWriterLock rwLock;

		// used for debugging
		private readonly string name;

		// when the number of entries in the hashtable gets larger than capacity,
		// the "stale" entries are removed and capacity is reset to twice the number
		// of remaining entries
		private int capacity;
		private readonly int originalCapacity;

		// time, in seconds, after which an entry is considerred stale (if the reference
		// count is zero)
		private TimeSpan staleTime;

		public CacheTable(string name, int capacity, TimeSpan staleTime)
		{
			originalCapacity = capacity;
			this.capacity = capacity;
			this.staleTime = staleTime;
			this.name = name;
			rwLock = new ReaderWriterLock();
			table = new Dictionary<Key, CacheEntry<Value>>();
		}

		public Value Get(Key key)
		{
			Value val = default;    // This keyword might change with C# compiler
			rwLock.AcquireReaderLock(-1);
			try
			{
				if (table.TryGetValue(key, out CacheEntry<Value> value))
				{
					CacheEntry<Value> entry = value;
					if (entry != null)
					{
						entry.timeStamp = DateTime.UtcNow;
						val = entry.contents;
					}
				}
			}
			finally
			{
				rwLock.ReleaseReaderLock();
			}
			return val;
		}

		public void Put(Key key, Value val)
		{
			rwLock.AcquireWriterLock(-1);
			try
			{
				if (val == null /* not Value.default - bug in C# compiler? */)
				{
					table[key] = null;
				}
				else
				{
					CacheEntry<Value> entry = null;
					if (table.TryGetValue(key, out CacheEntry<Value> value))
						entry = value; //which could be null also

					if (entry == null)
					{
						entry = new CacheEntry<Value>();
						table[key] = entry;
						if (table.Count >= capacity)
						{
							ClearStale(staleTime);
						}
					}
					entry.timeStamp = DateTime.UtcNow;
					entry.contents = val;
				}
			}
			finally
			{
				rwLock.ReleaseWriterLock();
			}
		}

		public void Remove(Key key)
		{
			rwLock.AcquireWriterLock(-1);
			try
			{
				if (table.ContainsKey(key))
					table.Remove(key);
			}
			finally
			{
				rwLock.ReleaseWriterLock();
			}
		}

		public void ClearStale(TimeSpan staleAge)
		{
			DateTime now = DateTime.UtcNow;
			Dictionary<Key, CacheEntry<Value>> newTable = new Dictionary<Key, CacheEntry<Value>>();

			rwLock.AcquireReaderLock(-1);
			try
			{
				foreach (KeyValuePair<Key, CacheEntry<Value>> kv in table)
				{
					CacheEntry<Value> iterEntry = kv.Value;

					// see if this entry is stale (ticks are 100 nano-sec.)
					if (now - iterEntry.timeStamp < staleAge)
					{
						newTable[kv.Key] = kv.Value;
					}
				}
			}
			finally
			{
				rwLock.ReleaseReaderLock();
			}

			rwLock.AcquireWriterLock(-1);
			table = newTable;
			capacity = 2 * table.Count;
			if (capacity < originalCapacity) capacity = originalCapacity;
			rwLock.ReleaseWriterLock();
		}

		private class CacheEntry<T>
		{
			public T contents;
			public DateTime timeStamp;
		}
	}

	internal class MQCacheableInfo
	{
		// Double-checked locking pattern requires volatile for read/write synchronization
		private volatile MessageQueueHandle readHandle = MessageQueueHandle.InvalidHandle;

		// Double-checked locking pattern requires volatile for read/write synchronization
		private volatile MessageQueueHandle writeHandle = MessageQueueHandle.InvalidHandle;
		private bool isTransactional;

		// Double-checked locking pattern requires volatile for read/write synchronization
		private volatile bool isTransactional_valid = false;

		// Double-checked locking pattern requires volatile for read/write synchronization
		private volatile bool boundToThreadPool;
		private readonly string formatName;
		private readonly int shareMode;
		private readonly QueueAccessModeHolder accessMode;
		private bool disposed;

		private readonly object syncRoot = new object();

		public MQCacheableInfo(string formatName, QueueAccessMode accessMode, int shareMode)
		{
			this.formatName = formatName;
			this.shareMode = shareMode;

			// For each accessMode, corresponding QueueAccessModeHolder is a singleton.
			// Call factory method to return existing holder for this access mode, 
			// or make a new one if noone used this access mode before.
			//
			this.accessMode = QueueAccessModeHolder.GetQueueAccessModeHolder(accessMode);
		}

		public bool CanRead
		{
			get
			{
				if (!accessMode.CanRead())
					return false;

				if (readHandle.IsInvalid)
				{
					if (disposed)
						throw new ObjectDisposedException(GetType().Name);

					lock (syncRoot)
					{
						if (readHandle.IsInvalid)
						{
							int status = UnsafeNativeMethods.MQOpenQueue(formatName, accessMode.GetReadAccessMode(), shareMode, out var result);
							if (IsFatalError(status))
								return false;

							readHandle = result;
						}
					}
				}

				return true;
			}
		}

		public bool CanWrite
		{
			get
			{
				if (!accessMode.CanWrite())
					return false;

				if (writeHandle.IsInvalid)
				{
					if (disposed)
						throw new ObjectDisposedException(GetType().Name);

					lock (syncRoot)
					{
						if (writeHandle.IsInvalid)
						{
							int status = UnsafeNativeMethods.MQOpenQueue(formatName, accessMode.GetWriteAccessMode(), 0, out var result);
							if (IsFatalError(status))
								return false;

							writeHandle = result;
						}
					}
				}

				return true;
			}
		}

		public int RefCount { get; private set; }

		public MessageQueueHandle ReadHandle
		{
			get
			{
				if (readHandle.IsInvalid)
				{
					if (disposed)
						throw new ObjectDisposedException(GetType().Name);

					lock (syncRoot)
					{
						if (readHandle.IsInvalid)
						{
							int status = UnsafeNativeMethods.MQOpenQueue(formatName, accessMode.GetReadAccessMode(), shareMode, out var result);
							if (IsFatalError(status))
								throw new MessageQueueException(status);

							readHandle = result;
						}
					}
				}

				return readHandle;
			}
		}

		public MessageQueueHandle WriteHandle
		{
			get
			{
				if (writeHandle.IsInvalid)
				{
					if (disposed)
						throw new ObjectDisposedException(GetType().Name);

					lock (syncRoot)
					{
						if (writeHandle.IsInvalid)
						{
							int status = UnsafeNativeMethods.MQOpenQueue(formatName, accessMode.GetWriteAccessMode(), 0, out var result);
							if (IsFatalError(status))
								throw new MessageQueueException(status);

							writeHandle = result;
						}
					}
				}

				return writeHandle;
			}
		}

		public bool Transactional
		{
			get
			{
				if (!isTransactional_valid)
				{
					lock (syncRoot)
					{
						if (!isTransactional_valid)
						{
							QueuePropertyVariants props = new QueuePropertyVariants();
							props.SetUI1(NativeMethods.QUEUE_PROPID_TRANSACTION, 0);
							int status = UnsafeNativeMethods.MQGetQueueProperties(formatName, props.Lock());
							props.Unlock();
							if (IsFatalError(status))
								throw new MessageQueueException(status);

							isTransactional = props.GetUI1(NativeMethods.QUEUE_PROPID_TRANSACTION) != NativeMethods.QUEUE_TRANSACTIONAL_NONE;
							isTransactional_valid = true;
						}
					}
				}

				return isTransactional;
			}
		}

		public void AddRef()
		{
			lock (this)
			{
				++RefCount;
			}
		}

		public void BindToThreadPool()
		{
			if (!boundToThreadPool)
			{
				lock (this)
				{
					if (!boundToThreadPool)
					{
						//SECREVIEW: At this point at least MessageQueue permission with Browse
						//                         access has already been demanded.
						SecurityPermission permission = new SecurityPermission(PermissionState.Unrestricted);
						permission.Assert();
						try
						{
							ThreadPool.BindHandle(ReadHandle);
						}
						finally
						{
							System.Security.CodeAccessPermission.RevertAssert();
						}

						boundToThreadPool = true;
					}
				}
			}
		}

		public void CloseIfNotReferenced()
		{
			lock (this)
			{
				if (RefCount == 0)
					Close();
			}
		}

		public void Close()
		{
			boundToThreadPool = false;
			if (!writeHandle.IsInvalid)
			{
				lock (syncRoot)
				{
					if (!writeHandle.IsInvalid)
					{
						writeHandle.Close();
					}
				}
			}
			if (!readHandle.IsInvalid)
			{
				lock (syncRoot)
				{
					if (!readHandle.IsInvalid)
					{
						readHandle.Close();
					}
				}
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				Close();
			}

			disposed = true;
		}

		public void Release()
		{
			lock (this)
			{
				--RefCount;
			}
		}
	}

	internal class QueueInfoKeyHolder
	{
		private readonly string formatName;
		private readonly QueueAccessMode accessMode;

		public QueueInfoKeyHolder(string formatName, QueueAccessMode accessMode)
		{
			this.formatName = formatName.ToUpper(CultureInfo.InvariantCulture);
			this.accessMode = accessMode;
		}

		public override int GetHashCode()
		{
			return formatName.GetHashCode() + (int)accessMode;
		}

		public override bool Equals(object obj)
		{
			if (obj == null || GetType() != obj.GetType()) return false;

			QueueInfoKeyHolder qik = (QueueInfoKeyHolder)obj;
			return Equals(qik);
		}

		public bool Equals(QueueInfoKeyHolder qik)
		{
			if (qik == null) return false;

			// string.Equals performs case-sensitive and culture-insensitive comparison
			// we address case sensitivity by normalizing format name in the constructor
			return accessMode == qik.accessMode && formatName.Equals(qik.formatName);
		}
	}
}
