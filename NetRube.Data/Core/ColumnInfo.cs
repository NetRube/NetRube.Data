// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: ColumnInfo.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

using System.Reflection;

/// <summary>
/// Data 命名空间
/// </summary>
namespace NetRube.Data
{
	/// <summary>
	/// 数据表字段信息
	/// </summary>
	/// <remarks>Typically ColumnInfo is automatically populated from the attributes on a POCO object and it's properties. It can
	/// however also be returned from the IMapper interface to provide your owning bindings between the DB and your POCOs.</remarks>
	public class ColumnInfo
	{
		/// <summary>
		/// 数据表字段名
		/// </summary>
		/// <value>数据表字段名</value>
		public string ColumnName
		{
			get;
			set;
		}

		/// <summary>
		/// 如果此字段是从数据库中返回的一个计算量，则将不会用于插入和更新操作
		/// </summary>
		/// <value>如果此字段是从数据库中返回的一个计算量，则该值为 <c>true</c>；否则为 <c>false</c>。</value>
		public bool ResultColumn
		{
			get;
			set;
		}

		/// <summary>
		/// 如果此字段是从数据库中的日期和时间，指定是否强制转换为 UTC
		/// </summary>
		/// <value>如果要强制转换为 UTC，则该值为 <c>true</c>；否则为 <c>false</c>。</value>
		public bool ForceToUtc
		{
			get;
			set;
		}

		/// <summary>
		/// 将实体属性转换为字段信息
		/// </summary>
		/// <param name="pi">实体属性</param>
		/// <returns>字段信息</returns>
		public static ColumnInfo FromProperty(PropertyInfo pi)
		{
			// 我项目中实体的虚属性不是数据表的字段，只作为附加信息。
			if(pi.IsVirtual_()) return null;

			// Check if declaring poco has [Explicit] attribute
			bool ExplicitColumns = pi.DeclaringType.GetCustomAttributes(typeof(ExplicitColumnsAttribute), true).Length > 0;

			// Check for [Column]/[Ignore] Attributes
			var ColAttrs = pi.GetCustomAttributes(typeof(ColumnAttribute), true);
			if(ExplicitColumns)
			{
				if(ColAttrs.Length == 0)
					return null;
			}
			else
			{
				if(pi.GetCustomAttributes(typeof(IgnoreAttribute), true).Length != 0)
					return null;
			}

			ColumnInfo ci = new ColumnInfo();

			// Read attribute
			if(ColAttrs.Length > 0)
			{
				var colattr = (ColumnAttribute)ColAttrs[0];

				ci.ColumnName = colattr.Name == null ? pi.Name : colattr.Name;
				ci.ForceToUtc = colattr.ForceToUtc;
				if((colattr as ResultColumnAttribute) != null)
					ci.ResultColumn = true;

			}
			else
			{
				ci.ColumnName = pi.Name;
				ci.ForceToUtc = false;
				ci.ResultColumn = false;
			}

			return ci;
		}
	}
}