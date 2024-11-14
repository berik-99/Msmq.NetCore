
using System;

namespace Msmq.NetCore.Messaging.Interop;

using Microsoft.Win32.SafeHandles;

internal class MessageQueueHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	public static readonly MessageQueueHandle InvalidHandle = new InvalidMessageQueueHandle();

	MessageQueueHandle() : base(true) { }

	protected override bool ReleaseHandle()
	{
		SafeNativeMethods.MQCloseQueue(handle);

		return true;
	}

	public override bool IsInvalid => base.IsInvalid || IsClosed;

	// A subclass needed to express InvalidHandle. The reason is that CLR notices that 
	// ReleaseHandle requires a call to MQRT.DLL, and throws in the ctor if MQRT.DLL is not available,
	// even though CTOR ITSELF DOES NOT REQUIRE MQRT.DLL. 
	// We address this by defining a NOOP ReleaseHandle
	sealed class InvalidMessageQueueHandle : MessageQueueHandle
	{
		protected override bool ReleaseHandle()
		{
			return true;
		}
	}
}

internal class CursorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	public static readonly CursorHandle NullHandle = new InvalidCursorHandle();

	protected CursorHandle() : base(true) { }

	protected override bool ReleaseHandle()
	{
		SafeNativeMethods.MQCloseCursor(handle);

		return true;
	}

	public override bool IsInvalid => base.IsInvalid || IsClosed;

	// A subclass needed to express InvalidHandle. The reason is that CLR notices that 
	// ReleaseHandle requires a call to MQRT.DLL, and throws in the ctor if MQRT.DLL is not available,
	// even though CTOR ITSELF DOES NOT REQUIRE MQRT.DLL. 
	// We address this by defining a NOOP ReleaseHandle
	sealed class InvalidCursorHandle : CursorHandle
	{
		protected override bool ReleaseHandle()
		{
			return true;
		}
	}
}

internal class LocatorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	public static readonly LocatorHandle InvalidHandle = new InvalidLocatorHandle();

	protected LocatorHandle() : base(true) { }

	protected override bool ReleaseHandle()
	{
		SafeNativeMethods.MQLocateEnd(handle);

		return true;
	}

	public override bool IsInvalid => base.IsInvalid || IsClosed;

	// A subclass needed to express InvalidHandle. The reason is that CLR notices that 
	// ReleaseHandle requires a call to MQRT.DLL, and throws in the ctor if MQRT.DLL is not available,
	// even though CTOR ITSELF DOES NOT REQUIRE MQRT.DLL. 
	// We address this by defining a NOOP ReleaseHandle
	sealed class InvalidLocatorHandle : LocatorHandle
	{
		protected override bool ReleaseHandle()
		{
			return true;
		}
	}
}

internal sealed class SecurityContextHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	internal SecurityContextHandle(IntPtr existingHandle)
		: base(true) => SetHandle(existingHandle);

	protected override bool ReleaseHandle()
	{
		SafeNativeMethods.MQFreeSecurityContext(handle);

		return true;
	}

	public override bool IsInvalid => base.IsInvalid || IsClosed;
}
