using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Msmq.NetCore.Messaging.Interop;

internal class Columns
{
	private readonly int maxCount;
	private readonly MQCOLUMNSET columnSet = new MQCOLUMNSET();

	public Columns(int maxCount)
	{
		this.maxCount = maxCount;
		columnSet.columnIdentifiers = Marshal.AllocHGlobal(maxCount * 4);
		columnSet.columnCount = 0;
	}

	public virtual void AddColumnId(int columnId)
	{
		lock (this)
		{
			if (columnSet.columnCount >= maxCount)
				throw new InvalidOperationException(Res.GetString(Res.TooManyColumns, maxCount.ToString(CultureInfo.CurrentCulture)));

			++columnSet.columnCount;
			columnSet.SetId(columnId, columnSet.columnCount - 1);
		}
	}

	public virtual MQCOLUMNSET GetColumnsRef()
	{
		return columnSet;
	}

	[StructLayout(LayoutKind.Sequential)]
	public class MQCOLUMNSET
	{
		public int columnCount;
		public IntPtr columnIdentifiers;

		~MQCOLUMNSET()
		{
			if (columnIdentifiers != (IntPtr)0)
			{
				Marshal.FreeHGlobal(columnIdentifiers);
				columnIdentifiers = (IntPtr)0;
			}
		}

		public virtual void SetId(int columnId, int index)
		{
			Marshal.WriteInt32((IntPtr)((long)columnIdentifiers + index * 4), columnId);
		}
	}
}
