using System;

namespace NetRube.Data
{
	/// <summary>表示此实体按所有标明为字段的属性进行映射</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class ExplicitColumnsAttribute : Attribute
	{
	}
}