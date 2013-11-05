// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: AutoSelectHelper.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Internal 命名空间
/// </summary>
namespace NetRube.Data.Internal
{
	static class AutoSelectHelper
	{
		public static string AddSelectClause<T>(DatabaseType DatabaseType, string sql)
		{
			if(sql.StartsWith(";"))
				return sql.Substring(1);

			if(!rxSelect.IsMatch(sql))
			{
				var pd = PocoData.ForType(typeof(T));
				var tableName = DatabaseType.EscapeTableName(pd.TableInfo.TableName);
				string cols = pd.Columns.Count != 0 ? string.Join(", ", (from c in pd.QueryColumns select tableName + "." + DatabaseType.EscapeSqlIdentifier(c)).ToArray()) : "NULL";
				if(!rxFrom.IsMatch(sql))
					sql = string.Format("SELECT {0} FROM {1} {2}", cols, tableName, sql);
				else
					sql = string.Format("SELECT {0} {1}", cols, sql);
			}
			return sql;
		}

		static Regex rxSelect = new Regex(@"\A\s*(SELECT|EXECUTE|CALL)\s", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
		static Regex rxFrom = new Regex(@"\A\s*FROM\s", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Multiline);
	}
}
