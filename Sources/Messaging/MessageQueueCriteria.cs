
using System;
using System.ComponentModel;
using System.Globalization;
using System.Security.Permissions;
using NetCore.Msmq.Messaging.Interop;

namespace NetCore.Msmq.Messaging;

public class MessageQueueCriteria
{
	private DateTime createdAfter;
	private DateTime createdBefore;
	private string label;
	private string machine;
	private DateTime modifiedAfter;
	private DateTime modifiedBefore;
	private Guid category;
	private readonly CriteriaPropertyFilter filter = new CriteriaPropertyFilter();
	private Restrictions restrictions;
	private Guid machineId;
	private static DateTime minDate = new DateTime(1970, 1, 1);
	private static DateTime maxDate = new DateTime(2038, 1, 19);

	public DateTime CreatedAfter
	{
		get
		{
			if (!filter.CreatedAfter)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return createdAfter;
		}

		set
		{
			if (value < minDate || value > maxDate)
				throw new ArgumentException(Res.GetString(Res.InvalidDateValue, minDate.ToString(CultureInfo.CurrentCulture), maxDate.ToString(CultureInfo.CurrentCulture)));

			createdAfter = value;
			if (filter.CreatedBefore && createdAfter > createdBefore)
				createdBefore = createdAfter;

			filter.CreatedAfter = true;
		}
	}

	public DateTime CreatedBefore
	{
		get
		{
			if (!filter.CreatedBefore)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return createdBefore;
		}

		set
		{
			if (value < minDate || value > maxDate)
				throw new ArgumentException(Res.GetString(Res.InvalidDateValue, minDate.ToString(CultureInfo.CurrentCulture), maxDate.ToString(CultureInfo.CurrentCulture)));

			createdBefore = value;
			if (filter.CreatedAfter && createdAfter > createdBefore)
				createdAfter = createdBefore;

			filter.CreatedBefore = true;
		}
	}

	internal bool FilterMachine => filter.MachineName;

	public string Label
	{
		get
		{
			if (!filter.Label)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return label;
		}

		set
		{
			label = value ?? throw new ArgumentNullException(nameof(value));
			filter.Label = true;
		}
	}

	public string MachineName
	{
		get
		{
			if (!filter.MachineName)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return machine;
		}

		set
		{
			if (!SyntaxCheck.CheckMachineName(value))
				throw new ArgumentException(Res.GetString(Res.InvalidProperty, "MachineName", value));

			//SECREVIEW: Setting this property shouldn't demmand any permissions,
			//                    the machine id will only be used internally.
			MessageQueuePermission permission = new MessageQueuePermission(PermissionState.Unrestricted);
			permission.Assert();
			try
			{
				machineId = MessageQueue.GetMachineId(value);
			}
			finally
			{
				System.Security.CodeAccessPermission.RevertAssert();
			}

			machine = value;
			filter.MachineName = true;
		}
	}

	public DateTime ModifiedAfter
	{
		get
		{
			if (!filter.ModifiedAfter)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return modifiedAfter;
		}

		set
		{
			if (value < minDate || value > maxDate)
				throw new ArgumentException(Res.GetString(Res.InvalidDateValue, minDate.ToString(CultureInfo.CurrentCulture), maxDate.ToString(CultureInfo.CurrentCulture)));

			modifiedAfter = value;

			if (filter.ModifiedBefore && modifiedAfter > modifiedBefore)
				modifiedBefore = modifiedAfter;

			filter.ModifiedAfter = true;
		}
	}

	public DateTime ModifiedBefore
	{
		get
		{
			if (!filter.ModifiedBefore)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return modifiedBefore;
		}

		set
		{
			if (value < minDate || value > maxDate)
				throw new ArgumentException(Res.GetString(Res.InvalidDateValue, minDate.ToString(CultureInfo.CurrentCulture), maxDate.ToString(CultureInfo.CurrentCulture)));

			modifiedBefore = value;

			if (filter.ModifiedAfter && modifiedAfter > modifiedBefore)
				modifiedAfter = modifiedBefore;

			filter.ModifiedBefore = true;
		}
	}

	internal Restrictions.MQRESTRICTION Reference
	{
		get
		{
			int size = 0;
			if (filter.CreatedAfter)
				++size;
			if (filter.CreatedBefore)
				++size;
			if (filter.Label)
				++size;
			if (filter.ModifiedAfter)
				++size;
			if (filter.ModifiedBefore)
				++size;
			if (filter.Category)
				++size;

			restrictions = new Restrictions(size);
			if (filter.CreatedAfter)
				restrictions.AddI4(NativeMethods.QUEUE_PROPID_CREATE_TIME, Restrictions.PRGT, ConvertTime(createdAfter));
			if (filter.CreatedBefore)
				restrictions.AddI4(NativeMethods.QUEUE_PROPID_CREATE_TIME, Restrictions.PRLE, ConvertTime(createdBefore));
			if (filter.Label)
				restrictions.AddString(NativeMethods.QUEUE_PROPID_LABEL, Restrictions.PREQ, label);
			if (filter.ModifiedAfter)
				restrictions.AddI4(NativeMethods.QUEUE_PROPID_MODIFY_TIME, Restrictions.PRGT, ConvertTime(modifiedAfter));
			if (filter.ModifiedBefore)
				restrictions.AddI4(NativeMethods.QUEUE_PROPID_MODIFY_TIME, Restrictions.PRLE, ConvertTime(modifiedBefore));
			if (filter.Category)
				restrictions.AddGuid(NativeMethods.QUEUE_PROPID_TYPE, Restrictions.PREQ, category);

			return restrictions.GetRestrictionsRef();
		}
	}

	public Guid Category
	{
		get
		{
			if (!filter.Category)
				throw new InvalidOperationException(Res.GetString(Res.CriteriaNotDefined));

			return category;
		}

		set
		{
			category = value;
			filter.Category = true;
		}
	}

	public void ClearAll()
	{
		filter.ClearAll();
	}

	private int ConvertTime(DateTime time)
	{
		time = time.ToUniversalTime();
		return (int)(time - minDate).TotalSeconds;
	}

	private class CriteriaPropertyFilter
	{
		public bool CreatedAfter;
		public bool CreatedBefore;
		public bool Label;
		public bool MachineName;
		public bool ModifiedAfter;
		public bool ModifiedBefore;
		public bool Category;

		public void ClearAll()
		{
			CreatedAfter = false;
			CreatedBefore = false;
			Label = false;
			MachineName = false;
			ModifiedAfter = false;
			ModifiedBefore = false;
			Category = false;
		}
	}
}
