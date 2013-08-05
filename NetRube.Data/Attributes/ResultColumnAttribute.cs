using System;

namespace NetRube.Data
{
	/// <summary>表示此属性为返回结果</summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ResultColumnAttribute : ColumnAttribute
	{
		/// <summary>初始化一个新 <see cref="ResultColumnAttribute"/> 实例。</summary>
		public ResultColumnAttribute() { }

		/// <summary>初始化一个新 <see cref="ResultColumnAttribute"/> 实例。</summary>
		/// <param name="name">数据表字段名称</param>
		public ResultColumnAttribute(string name)
			: base(name) { }
	}
}