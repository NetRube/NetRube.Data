// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Reflection;

namespace NetRube.Data
{
	/// <summary>定义数据源到实体的映射接口</summary>
	/// <remarks>
	/// To use this functionality, instantiate a class that implements IMapper and then pass it to
	/// PetaPoco through the static method Mappers.Register()
	/// </remarks>
	public interface IMapper
	{
		/// <summary>获取数据表信息</summary>
		/// <param name="pocoType">实体类型</param>
		/// <returns>数据表信息</returns>
		/// <remarks>
		/// This method must return a valid TableInfo.
		/// To create a TableInfo from a POCO's attributes, use TableInfo.FromPoco
		/// </remarks>
		TableInfo GetTableInfo(Type pocoType);

		/// <summary>获取数据表字段信息</summary>
		/// <param name="pocoProperty">实体属性信息</param>
		/// <returns>数据表字段信息</returns>
		/// <remarks>
		/// To create a ColumnInfo from a property's attributes, use PropertyInfo.FromProperty
		/// </remarks>
		ColumnInfo GetColumnInfo(PropertyInfo pocoProperty);

		/// <summary>获取将数据源值到实体属性值的转换器</summary>
		/// <param name="TargetProperty">目标实体属性信息</param>
		/// <param name="SourceType">数据源返回来的值的类型</param>
		/// <returns>数据源值到实体属性值的转换器</returns>
		Func<object, object> GetFromDbConverter(PropertyInfo TargetProperty, Type SourceType);

		/// <summary>获取将实体属性值到数据源对应值的转换器</summary>
		/// <param name="SourceProperty">实体属性信息</param>
		/// <returns>实体属性值到数据源对应值的转换器</returns>
		/// <remarks>
		/// This conversion is only used for converting values from POCO's that are
		/// being Inserted or Updated.
		/// Conversion is not available for parameter values passed directly to queries.
		/// </remarks>
		Func<object, object> GetToDbConverter(PropertyInfo SourceProperty);
	}
}