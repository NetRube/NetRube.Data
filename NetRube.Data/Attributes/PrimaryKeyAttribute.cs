using System;

namespace NetRube.Data
{
	/// <summary>标明实体中作为数据表主键的属性</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class PrimaryKeyAttribute : Attribute
	{
		/// <summary>初始化一个新 <see cref="PrimaryKeyAttribute"/> 实例。</summary>
		/// <param name="primaryKey">主键名称</param>
		public PrimaryKeyAttribute(string primaryKey)
		{
			Value = primaryKey;
			autoIncrement = true;
		}

		/// <summary>获取主键名称</summary>
		/// <value>主键名称</value>
		public string Value { get; private set; }

		/// <summary>获取或设置作为 Oracle 自增主键的序列字段名</summary>
		/// <value>Oracle 自增主键的序列字段名</value>
		public string sequenceName { get; set; }

		/// <summary>获取或设置一个值，该值指示是否为自增主键</summary>
		/// <value>如果为自增主键，则该值为 <c>true</c>；否则为 <c>false</c>。</value>
		public bool autoIncrement { get; set; }
	}
}