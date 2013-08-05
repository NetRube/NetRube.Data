using System;

namespace NetRube.Data
{
	/// <summary>表示此属性不与数据表字段映射</summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class IgnoreAttribute : Attribute
	{
	}
}