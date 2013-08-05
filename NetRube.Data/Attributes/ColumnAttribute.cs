using System;

namespace NetRube.Data
{
	/// <summary>表示此属性为数据表字段，并可以另指定字段名称</summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ColumnAttribute : Attribute
	{
		/// <summary>初始化一个新 <see cref="ColumnAttribute"/> 实例。</summary>
		public ColumnAttribute()
		{
			ForceToUtc = false;
		}

		/// <summary>初始化一个新 <see cref="ColumnAttribute"/> 实例。</summary>
		/// <param name="Name">数据表字段名称</param>
		public ColumnAttribute(string Name)
		{
			this.Name = Name;
			ForceToUtc = false;
		}

		/// <summary>获取或设置数据表字段名称</summary>
		/// <value>数据表字段名称</value>
		public string Name { get; set; }

		/// <summary>
		/// 获取或设置一个值，该值指示是否将时间转换为 UTC
		/// </summary>
		/// <value>如果要转换为 UTC，则该值为 <c>true</c>；否则为 <c>false</c>。</value>
		public bool ForceToUtc { get; set; }
	}
}