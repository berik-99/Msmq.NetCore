using System;
using System.Runtime.InteropServices;

namespace NetCore.Msmq.Messaging.Interop;

internal class Restrictions
{
	private readonly MQRESTRICTION restrictionStructure;
	public const int PRLT = 0;
	public const int PRLE = 1;
	public const int PRGT = 2;
	public const int PRGE = 3;
	public const int PREQ = 4;
	public const int PRNE = 5;

	public Restrictions(int maxRestrictions) => restrictionStructure = new MQRESTRICTION(maxRestrictions);

	public virtual void AddGuid(int propertyId, int op, Guid value)
	{
		IntPtr data = Marshal.AllocHGlobal(16);
		Marshal.Copy(value.ToByteArray(), 0, data, 16);
		AddItem(propertyId, op, MessagePropertyVariants.VT_CLSID, data);
	}

	public virtual void AddGuid(int propertyId, int op)
	{
		IntPtr data = Marshal.AllocHGlobal(16);
		AddItem(propertyId, op, MessagePropertyVariants.VT_CLSID, data);
	}

	private void AddItem(int id, int op, short vt, IntPtr data)
	{
		Marshal.WriteInt32(restrictionStructure.GetNextValidPtr(0), op);
		Marshal.WriteInt32(restrictionStructure.GetNextValidPtr(4), id);
		Marshal.WriteInt16(restrictionStructure.GetNextValidPtr(8), vt);
		Marshal.WriteInt16(restrictionStructure.GetNextValidPtr(10), 0);
		Marshal.WriteInt16(restrictionStructure.GetNextValidPtr(12), 0);
		Marshal.WriteInt16(restrictionStructure.GetNextValidPtr(14), 0);
		Marshal.WriteIntPtr(restrictionStructure.GetNextValidPtr(16), data);
		Marshal.WriteIntPtr(restrictionStructure.GetNextValidPtr(16 + IntPtr.Size), (IntPtr)0);
		++restrictionStructure.restrictionCount;
	}

	public virtual void AddI4(int propertyId, int op, int value)
	{
		AddItem(propertyId, op, MessagePropertyVariants.VT_I4, (IntPtr)value);
	}

	public virtual void AddString(int propertyId, int op, string value)
	{
		if (value == null)
		{
			AddItem(propertyId, op, MessagePropertyVariants.VT_NULL, (IntPtr)0);
		}
		else
		{
			IntPtr data = Marshal.StringToHGlobalUni(value);
			AddItem(propertyId, op, MessagePropertyVariants.VT_LPWSTR, data);
		}
	}

	public virtual MQRESTRICTION GetRestrictionsRef()
	{
		return restrictionStructure;
	}

	[StructLayout(LayoutKind.Sequential)]
	public class MQRESTRICTION
	{
		public int restrictionCount;
		public IntPtr restrinctions;

		public MQRESTRICTION(int maxCount) => restrinctions = Marshal.AllocHGlobal(maxCount * GetRestrictionSize());

		~MQRESTRICTION()
		{
			if (restrinctions != (IntPtr)0)
			{
				for (int index = 0; index < restrictionCount; ++index)
				{
					short vt = Marshal.ReadInt16((IntPtr)((long)restrinctions + index * GetRestrictionSize() + 8));
					if (vt != MessagePropertyVariants.VT_I4)
					{
						IntPtr dataPtr = (IntPtr)((long)restrinctions + index * GetRestrictionSize() + 16);
						IntPtr data = Marshal.ReadIntPtr(dataPtr);
						Marshal.FreeHGlobal(data);
					}
				}

				Marshal.FreeHGlobal(restrinctions);
				restrinctions = (IntPtr)0;
			}
		}

		public IntPtr GetNextValidPtr(int offset)
		{
			return (IntPtr)((long)restrinctions + restrictionCount * GetRestrictionSize() + offset);
		}

		public static int GetRestrictionSize()
		{
			return 16 + IntPtr.Size * 2;
		}
	}
}
