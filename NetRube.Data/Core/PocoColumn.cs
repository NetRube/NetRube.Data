﻿// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Reflection;

namespace NetRube.Data.Internal
{
	internal class PocoColumn
	{
		public string ColumnName;
		public PropertyInfo PropertyInfo;
		public bool ResultColumn;
		public bool ForceToUtc;
		public virtual void SetValue(object target, object val) { PropertyInfo.FastSetValue(target, val); }
		public virtual object GetValue(object target) { return PropertyInfo.FastGetValue(target); }
		public virtual object ChangeType(object val) { return Convert.ChangeType(val, PropertyInfo.PropertyType); }
	}
}