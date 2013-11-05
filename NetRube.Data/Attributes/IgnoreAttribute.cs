// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: IgnoreAttribute.cs
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
	/// 表示此属性不与数据表字段映射
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class IgnoreAttribute : Attribute
	{
	}
}