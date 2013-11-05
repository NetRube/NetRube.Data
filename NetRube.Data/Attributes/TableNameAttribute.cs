// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: TableNameAttribute.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

using System;

/// <summary>
/// Data 命名空间
/// </summary>
namespace NetRube.Data
{
	/// <summary>
	/// 指定数据表名称
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class TableNameAttribute : Attribute
	{
		/// <summary>
		/// 初始化一个新 <see cref="TableNameAttribute" /> 实例。
		/// </summary>
		/// <param name="tableName">数据表名称</param>
		public TableNameAttribute(string tableName)
		{
			Value = tableName;
		}

		/// <summary>
		/// 获取数据表名称
		/// </summary>
		/// <value>数据表名称</value>
		public string Value { get; private set; }
	}
}