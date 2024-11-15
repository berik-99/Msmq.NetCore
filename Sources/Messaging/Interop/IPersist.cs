using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Msmq.NetCore.Messaging.Interop;

[ComImport,
Guid("0000010C-0000-0000-C000-000000000046"),
InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersist
{
	[SuppressUnmanagedCodeSecurity]
	void GetClassID([Out] out Guid pClassID);
}
