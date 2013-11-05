// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: ColumnAttribute.cs
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
	/// 表示此属性为数据表字段，并可以另指定字段名称
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ColumnAttribute : Attribute
	{
		/// <summary>
		/// 初始化一个新 <see cref="ColumnAttribute" /> 实例。
		/// </summary>
		public ColumnAttribute()
		{
			ForceToUtc = false;
		}

		/// <summary>
		/// 初始化一个新 <see cref="ColumnAttribute" /> 实例。
		/// </summary>
		/// <param name="Name">数据表字段名称</param>
		public ColumnAttribute(string Name)
		{
			this.Name = Name;
			ForceToUtc = false;
		}

		/// <summary>
		/// 获取或设置数据表字段名称
		/// </summary>
		/// <value>数据表字段名称</value>
		public string Name { get; set; }

		/// <summary>
		/// 获取或设置一个值，该值指示是否将时间转换为 UTC
		/// </summary>
		/// <value>如果要转换为 UTC，则该值为 <c>true</c>；否则为 <c>false</c>。</value>
		public bool ForceToUtc { get; set; }
	}
}