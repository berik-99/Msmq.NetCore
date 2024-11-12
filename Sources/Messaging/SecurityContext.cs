
using System;
using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public sealed class SecurityContext : IDisposable
{
	readonly SecurityContextHandle handle;
	bool disposed;

	internal SecurityContext(SecurityContextHandle securityContext) => handle = securityContext;

	internal SecurityContextHandle Handle
	{
		get
		{
			if (disposed)
				throw new ObjectDisposedException(GetType().Name);

			return handle;
		}
	}

	public void Dispose()
	{
		handle.Close();
		disposed = true;
	}
}
