
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

[Serializable]
public class MessageQueueException : ExternalException, ISerializable
{
	private readonly int nativeErrorCode;

	internal MessageQueueException(int error) => nativeErrorCode = error;

	protected MessageQueueException(SerializationInfo info, StreamingContext context)
		: base(info, context) => nativeErrorCode = info.GetInt32("NativeErrorCode");

	public MessageQueueErrorCode MessageQueueErrorCode => (MessageQueueErrorCode)nativeErrorCode;

	public override string Message
	{
		get
		{
			try
			{
				return Res.GetString(Convert.ToString(nativeErrorCode, 16).ToUpper(CultureInfo.InvariantCulture));
			}
			catch
			{
				return GetUnknownErrorMessage(nativeErrorCode);
			}
		}
	}

	private static string GetUnknownErrorMessage(int error)
	{
		//get the system error message...
		string errorMsg = "";

		StringBuilder sb = new StringBuilder(256);
		int result = SafeNativeMethods.FormatMessage(SafeNativeMethods.FORMAT_MESSAGE_IGNORE_INSERTS |
								   SafeNativeMethods.FORMAT_MESSAGE_FROM_SYSTEM |
								   SafeNativeMethods.FORMAT_MESSAGE_ARGUMENT_ARRAY,
								   IntPtr.Zero, error, 0, sb, sb.Capacity + 1, IntPtr.Zero);
		if (result != 0)
		{
			int i = sb.Length;
			while (i > 0)
			{
				char ch = sb[i - 1];
				if (ch > 32 && ch != '.') break;
				i--;
			}
			errorMsg = sb.ToString(0, i);
		}
		else
		{
			errorMsg = Res.GetString("UnknownError", Convert.ToString(error, 16));
		}

		return errorMsg;
	}

	[SecurityCritical]
	public override void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		if (info == null)
		{
			throw new ArgumentNullException(nameof(info));
		}
		info.AddValue("NativeErrorCode", nativeErrorCode);
		base.GetObjectData(info, context);
	}
}
