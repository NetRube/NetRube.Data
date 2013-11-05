// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: ExplicitColumnsAttribute.cs
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
	/// 表示此实体按所有标明为字段的属性进行映射
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class ExplicitColumnsAttribute : Attribute
	{
	}
}