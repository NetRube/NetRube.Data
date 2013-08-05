using System;

namespace NetRube.Data
{
	/// <summary>指定数据表名称</summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class TableNameAttribute : Attribute
	{
		/// <summary>初始化一个新 <see cref="TableNameAttribute"/> 实例。</summary>
		/// <param name="tableName">数据表名称</param>
		public TableNameAttribute(string tableName)
		{
			Value = tableName;
		}

		/// <summary>获取数据表名称</summary>
		/// <value>数据表名称</value>
		public string Value { get; private set; }
	}
}