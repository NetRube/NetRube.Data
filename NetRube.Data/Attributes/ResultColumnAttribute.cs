// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: ResultColumnAttribute.cs
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
	/// 表示此属性为返回结果
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ResultColumnAttribute : ColumnAttribute
	{
		/// <summary>
		/// 初始化一个新 <see cref="ResultColumnAttribute" /> 实例。
		/// </summary>
		public ResultColumnAttribute() { }

		/// <summary>
		/// 初始化一个新 <see cref="ResultColumnAttribute" /> 实例。
		/// </summary>
		/// <param name="name">数据表字段名称</param>
		public ResultColumnAttribute(string name)
			: base(name) { }
	}
}