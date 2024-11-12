using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public class ActiveXMessageFormatter : IMessageFormatter
{
	internal const short VT_ARRAY = 0x2000;
	internal const short VT_BOOL = 11;
	internal const short VT_BSTR = 8;
	internal const short VT_CLSID = 72;
	internal const short VT_CY = 6;
	internal const short VT_DATE = 7;
	internal const short VT_I1 = 16;
	internal const short VT_I2 = 2;
	internal const short VT_I4 = 3;
	internal const short VT_I8 = 20;
	internal const short VT_LPSTR = 30;
	internal const short VT_LPWSTR = 31;
	internal const short VT_NULL = 1;
	internal const short VT_R4 = 4;
	internal const short VT_R8 = 5;
	internal const short VT_STREAMED_OBJECT = 68;
	internal const short VT_STORED_OBJECT = 69;
	internal const short VT_UI1 = 17;
	internal const short VT_UI2 = 18;
	internal const short VT_UI4 = 19;
	internal const short VT_UI8 = 21;
	internal const short VT_VECTOR = 0x1000;
	private byte[] internalBuffer;
	private UnicodeEncoding unicodeEncoding;
	private ASCIIEncoding asciiEncoding;
	private char[] internalCharBuffer;

	public bool CanRead(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		int variantType = message.BodyType;
		return variantType == VT_BOOL || variantType == VT_CLSID ||
			variantType == VT_CY || variantType == VT_DATE ||
			variantType == VT_I1 || variantType == VT_UI1 ||
			variantType == VT_I2 || variantType == VT_UI2 ||
			variantType == VT_I4 || variantType == VT_UI4 ||
			variantType == VT_I8 || variantType == VT_UI8 ||
			variantType == VT_NULL || variantType == VT_R4 ||
			variantType == VT_I8 || variantType == VT_STREAMED_OBJECT ||
			variantType == VT_STORED_OBJECT ||
			variantType == (VT_VECTOR | VT_UI1) ||
			variantType == VT_LPSTR || variantType == VT_LPWSTR ||
			variantType == VT_BSTR || variantType == VT_R8;
	}

	public object Clone()
	{
		return new ActiveXMessageFormatter();
	}

	public static void InitStreamedObject(object streamedObject)
	{
		if (streamedObject is IPersistStreamInit persistStreamInit)
			persistStreamInit.InitNew();
	}

	public object Read(Message message)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		Stream stream;
		byte[] bytes;
		byte[] newBytes;
		int size;
		int variantType = message.BodyType;
		switch (variantType)
		{
			case VT_LPSTR:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				size = message.properties.GetUI4(NativeMethods.MESSAGE_PROPID_BODY_SIZE);

				if (internalCharBuffer == null || internalCharBuffer.Length < size)
					internalCharBuffer = new char[size];

				if (asciiEncoding == null)
					asciiEncoding = new ASCIIEncoding();

				asciiEncoding.GetChars(bytes, 0, size, internalCharBuffer, 0);
				return new string(internalCharBuffer, 0, size);
			case VT_BSTR:
			case VT_LPWSTR:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				size = message.properties.GetUI4(NativeMethods.MESSAGE_PROPID_BODY_SIZE) / 2;

				if (internalCharBuffer == null || internalCharBuffer.Length < size)
					internalCharBuffer = new char[size];

				if (unicodeEncoding == null)
					unicodeEncoding = new UnicodeEncoding();

				unicodeEncoding.GetChars(bytes, 0, size * 2, internalCharBuffer, 0);
				return new string(internalCharBuffer, 0, size);
			case VT_VECTOR | VT_UI1:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				size = message.properties.GetUI4(NativeMethods.MESSAGE_PROPID_BODY_SIZE);
				newBytes = new byte[size];
				Array.Copy(bytes, newBytes, size);

				return newBytes;
			case VT_BOOL:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				newBytes = new byte[1];
				Array.Copy(bytes, newBytes, 1);
				return bytes[0] != 0;
			case VT_CLSID:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				newBytes = new byte[16];
				Array.Copy(bytes, newBytes, 16);
				return new Guid(newBytes);
			case VT_CY:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				newBytes = new byte[8];
				Array.Copy(bytes, newBytes, 8);
				return decimal.FromOACurrency(BitConverter.ToInt64(newBytes, 0));
			case VT_DATE:
				bytes = message.properties.GetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY);
				newBytes = new byte[8];
				Array.Copy(bytes, newBytes, 8);
				return new DateTime(BitConverter.ToInt64(newBytes, 0));
			case VT_I1:
			case VT_UI1:
				stream = message.BodyStream;
				bytes = new byte[1];
				stream.Read(bytes, 0, 1);
				return bytes[0];
			case VT_I2:
				stream = message.BodyStream;
				bytes = new byte[2];
				stream.Read(bytes, 0, 2);
				return BitConverter.ToInt16(bytes, 0);
			case VT_UI2:
				stream = message.BodyStream;
				bytes = new byte[2];
				stream.Read(bytes, 0, 2);
				return BitConverter.ToUInt16(bytes, 0);
			case VT_I4:
				stream = message.BodyStream;
				bytes = new byte[4];
				stream.Read(bytes, 0, 4);
				return BitConverter.ToInt32(bytes, 0);
			case VT_UI4:
				stream = message.BodyStream;
				bytes = new byte[4];
				stream.Read(bytes, 0, 4);
				return BitConverter.ToUInt32(bytes, 0);
			case VT_I8:
				stream = message.BodyStream;
				bytes = new byte[8];
				stream.Read(bytes, 0, 8);
				return BitConverter.ToInt64(bytes, 0);
			case VT_UI8:
				stream = message.BodyStream;
				bytes = new byte[8];
				stream.Read(bytes, 0, 8);
				return BitConverter.ToUInt64(bytes, 0);
			case VT_R4:
				stream = message.BodyStream;
				bytes = new byte[4];
				stream.Read(bytes, 0, 4);
				return BitConverter.ToSingle(bytes, 0);
			case VT_R8:
				stream = message.BodyStream;
				bytes = new byte[8];
				stream.Read(bytes, 0, 8);
				return BitConverter.ToDouble(bytes, 0);
			case VT_NULL:
				return null;
			case VT_STREAMED_OBJECT:
				stream = message.BodyStream;
				ComStreamFromDataStream comStream = new ComStreamFromDataStream(stream);
				return NativeMethods.OleLoadFromStream(comStream, ref NativeMethods.IID_IUnknown);
			case VT_STORED_OBJECT:
				throw new NotSupportedException(Res.GetString(Res.StoredObjectsNotSupported));
			default:
				throw new InvalidOperationException(Res.GetString(Res.InvalidTypeDeserialization));
		}
	}

	public void Write(Message message, object obj)
	{
		if (message == null)
			throw new ArgumentNullException(nameof(message));

		Stream stream;
		int variantType;
		if (obj is string s)
		{
			int size = s.Length * 2;
			if (internalBuffer == null || internalBuffer.Length < size)
				internalBuffer = new byte[size];

			if (unicodeEncoding == null)
				unicodeEncoding = new UnicodeEncoding();

			unicodeEncoding.GetBytes(s.ToCharArray(), 0, size / 2, internalBuffer, 0);
			message.properties.SetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY, internalBuffer);
			message.properties.AdjustSize(NativeMethods.MESSAGE_PROPID_BODY, size);
			message.properties.SetUI4(NativeMethods.MESSAGE_PROPID_BODY_SIZE, size);
			message.properties.SetUI4(NativeMethods.MESSAGE_PROPID_BODY_TYPE, VT_LPWSTR);
			return;
		}
		else if (obj is byte[])
		{
			byte[] bytes = (byte[])obj;
			if (internalBuffer == null || internalBuffer.Length < bytes.Length)
				internalBuffer = new byte[bytes.Length];

			Array.Copy(bytes, internalBuffer, bytes.Length);
			message.properties.SetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY, internalBuffer);
			message.properties.AdjustSize(NativeMethods.MESSAGE_PROPID_BODY, bytes.Length);
			message.properties.SetUI4(NativeMethods.MESSAGE_PROPID_BODY_SIZE, bytes.Length);
			message.properties.SetUI4(NativeMethods.MESSAGE_PROPID_BODY_TYPE, VT_UI1 | VT_VECTOR);
			return;
		}
		else if (obj is char[] chars)
		{
			int size = chars.Length * 2;
			if (internalBuffer == null || internalBuffer.Length < size)
				internalBuffer = new byte[size];

			(unicodeEncoding ?? (unicodeEncoding = new UnicodeEncoding())).GetBytes(chars, 0, size / 2, internalBuffer, 0);
			message.properties.SetUI1Vector(NativeMethods.MESSAGE_PROPID_BODY, internalBuffer);
			message.properties.SetUI4(NativeMethods.MESSAGE_PROPID_BODY_SIZE, size);
			message.properties.SetUI4(NativeMethods.MESSAGE_PROPID_BODY_TYPE, VT_LPWSTR);
			return;
		}
		else if (obj is byte b)
		{
			stream = new MemoryStream(1);
			stream.Write(new byte[] { b }, 0, 1);
			variantType = VT_UI1;
		}
		else if (obj is bool b1)
		{
			stream = new MemoryStream(1);
			if (b1)
				stream.Write(new byte[] { 0xff }, 0, 1);
			else
				stream.Write(new byte[] { 0x00 }, 0, 1);
			variantType = VT_BOOL;
		}
		else if (obj is char)
		{
			stream = new MemoryStream(2);
			byte[] bytes = BitConverter.GetBytes((char)obj);
			stream.Write(bytes, 0, 2);
			variantType = VT_UI2;
		}
		else if (obj is decimal @decimal)
		{
			stream = new MemoryStream(8);
			byte[] bytes = BitConverter.GetBytes(decimal.ToOACurrency(@decimal));
			stream.Write(bytes, 0, 8);
			variantType = VT_CY;
		}
		else if (obj is DateTime time)
		{
			stream = new MemoryStream(8);
			byte[] bytes = BitConverter.GetBytes(time.Ticks);
			stream.Write(bytes, 0, 8);
			variantType = VT_DATE;
		}
		else if (obj is double d)
		{
			stream = new MemoryStream(8);
			byte[] bytes = BitConverter.GetBytes(d);
			stream.Write(bytes, 0, 8);
			variantType = VT_R8;
		}
		else if (obj is short)
		{
			stream = new MemoryStream(2);
			byte[] bytes = BitConverter.GetBytes((short)obj);
			stream.Write(bytes, 0, 2);
			variantType = VT_I2;
		}
		else if (obj is ushort @ushort)
		{
			stream = new MemoryStream(2);
			byte[] bytes = BitConverter.GetBytes(@ushort);
			stream.Write(bytes, 0, 2);
			variantType = VT_UI2;
		}
		else if (obj is int i)
		{
			stream = new MemoryStream(4);
			byte[] bytes = BitConverter.GetBytes(i);
			stream.Write(bytes, 0, 4);
			variantType = VT_I4;
		}
		else if (obj is uint u)
		{
			stream = new MemoryStream(4);
			byte[] bytes = BitConverter.GetBytes(u);
			stream.Write(bytes, 0, 4);
			variantType = VT_UI4;
		}
		else if (obj is long l)
		{
			stream = new MemoryStream(8);
			byte[] bytes = BitConverter.GetBytes(l);
			stream.Write(bytes, 0, 8);
			variantType = VT_I8;
		}
		else if (obj is ulong @ulong)
		{
			stream = new MemoryStream(8);
			byte[] bytes = BitConverter.GetBytes(@ulong);
			stream.Write(bytes, 0, 8);
			variantType = VT_UI8;
		}
		else if (obj is float)
		{
			stream = new MemoryStream(4);
			byte[] bytes = BitConverter.GetBytes((float)obj);
			stream.Write(bytes, 0, 4);
			variantType = VT_R4;
		}
		else if (obj is IPersistStream pstream)
		{
			ComStreamFromDataStream comStream = new ComStreamFromDataStream(new MemoryStream());
			NativeMethods.OleSaveToStream(pstream, comStream);
			stream = comStream.GetDataStream();
			variantType = VT_STREAMED_OBJECT;
		}
		else if (obj == null)
		{
			stream = new MemoryStream();
			variantType = VT_NULL;
		}
		else
		{
			throw new InvalidOperationException(Res.GetString(Res.InvalidTypeSerialization));
		}

		message.BodyStream = stream;
		message.BodyType = variantType;
	}

	[ComVisible(false)]
	private class ComStreamFromDataStream : IStream
	{
		private readonly Stream dataStream;

		// to support seeking ahead of the stream length...
		private long virtualPosition = -1;

		public ComStreamFromDataStream(Stream dataStream) => this.dataStream = dataStream ?? throw new ArgumentNullException(nameof(dataStream));

		private void ActualizeVirtualPosition()
		{
			if (virtualPosition == -1) return;

			if (virtualPosition > dataStream.Length)
				dataStream.SetLength(virtualPosition);

			dataStream.Position = virtualPosition;

			virtualPosition = -1;
		}

		public IStream Clone()
		{
			NotImplemented();
			return null;
		}

		public void Commit(int grfCommitFlags)
		{
			dataStream.Flush();
			// Extend the length of the file if needed.
			ActualizeVirtualPosition();
		}

		public long CopyTo(IStream pstm, long cb, long[] pcbRead)
		{
			const int bufSize = 4096;
			IntPtr buffer = Marshal.AllocHGlobal((IntPtr)bufSize);
			if (buffer == IntPtr.Zero) throw new OutOfMemoryException();
			long written = 0;
			try
			{
				while (written < cb)
				{
					int toRead = bufSize;
					if (written + toRead > cb) toRead = (int)(cb - written);
					int read = Read(buffer, toRead);
					if (read == 0) break;
					if (pstm.Write(buffer, read) != read)
					{
						throw EFail(Res.GetString(Res.IncorrectNumberOfBytes));
					}
					written += read;
				}
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
			if (pcbRead?.Length > 0)
			{
				pcbRead[0] = written;
			}

			return written;
		}

		public Stream GetDataStream()
		{
			return dataStream;
		}

		public void LockRegion(long libOffset, long cb, int dwLockType)
		{
		}

		protected static ExternalException EFail(string msg)
		{
			ExternalException e = new ExternalException(msg, NativeMethods.E_FAIL);
			throw e;
		}

		protected static void NotImplemented()
		{
			ExternalException e = new ExternalException(Res.GetString(Res.NotImplemented), NativeMethods.E_NOTIMPL);
			throw e;
		}

		public int Read(IntPtr buf, int length)
		{
			byte[] buffer = new byte[length];
			int count = Read(buffer, length);
			Marshal.Copy(buffer, 0, buf, length);
			return count;
		}

		public int Read(byte[] buffer, int length)
		{
			ActualizeVirtualPosition();
			return dataStream.Read(buffer, 0, length);
		}

		public void Revert()
		{
			NotImplemented();
		}

		public long Seek(long offset, int origin)
		{
			long pos = virtualPosition;
			if (virtualPosition == -1)
			{
				pos = dataStream.Position;
			}
			long len = dataStream.Length;
			switch (origin)
			{
				case NativeMethods.STREAM_SEEK_SET:
					if (offset <= len)
					{
						dataStream.Position = offset;
						virtualPosition = -1;
					}
					else
					{
						virtualPosition = offset;
					}
					break;
				case NativeMethods.STREAM_SEEK_END:
					if (offset <= 0)
					{
						dataStream.Position = len + offset;
						virtualPosition = -1;
					}
					else
					{
						virtualPosition = len + offset;
					}
					break;
				case NativeMethods.STREAM_SEEK_CUR:
					if (offset + pos <= len)
					{
						dataStream.Position = pos + offset;
						virtualPosition = -1;
					}
					else
					{
						virtualPosition = offset + pos;
					}
					break;
			}
			if (virtualPosition != -1)
			{
				return virtualPosition;
			}
			else
			{
				return dataStream.Position;
			}
		}

		public void SetSize(long value)
		{
			dataStream.SetLength(value);
		}

		public void Stat(IntPtr pstatstg, int grfStatFlag)
		{
			// GpStream has a partial implementation, but it's so partial rather 
			// restrict it to use with GDI+
			NotImplemented();
		}

		public void UnlockRegion(long libOffset, long cb, int dwLockType)
		{
		}

		public int Write(IntPtr buf, int length)
		{
			byte[] buffer = new byte[length];
			Marshal.Copy(buf, buffer, 0, length);
			return Write(buffer, length);
		}

		public int Write(byte[] buffer, int length)
		{
			ActualizeVirtualPosition();
			dataStream.Write(buffer, 0, length);
			return length;
		}
	}
}
