// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: PocoColumn.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Reflection;

/// <summary>
/// Internal 命名空间
/// </summary>
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