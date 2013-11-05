// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: DatabaseType.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NetRube.Data.DatabaseTypes;

/// <summary>
/// Internal 命名空间
/// </summary>
namespace NetRube.Data.Internal
{
	/// <summary>
	/// 数据源类型基类
	/// </summary>
	abstract class DatabaseType
	{
		/// <summary>
		/// 获取 SQL 参数名称前缀
		/// </summary>
		/// <param name="ConnectionString">数据源连接字符串</param>
		/// <returns>参数名称前缀</returns>
		public virtual string GetParameterPrefix(string ConnectionString)
		{
			return "@";
		}

		/// <summary>
		/// 将 C# 数据类型转换为相应数据源的数据类型
		/// </summary>
		/// <param name="value">要转换的值</param>
		/// <returns>转换后的值</returns>
		public virtual object MapParameterValue(object value)
		{
			// Cast bools to integer
			if(value.GetType() == typeof(bool))
			{
				return ((bool)value) ? 1 : 0;
			}

			// Leave it
			return value;
		}

		/// <summary>
		/// 在命令执行前对命令进行修改
		/// </summary>
		/// <param name="cmd">命令</param>
		public virtual void PreExecute(IDbCommand cmd) { }

		/// <summary>
		/// 生成 SQL 分页查询语句
		/// </summary>
		/// <param name="skip">要跳过记录数量</param>
		/// <param name="take">要获取记录数</param>
		/// <param name="parts">原始 SQL 查询语句被解析后的组成部分</param>
		/// <param name="args">SQL 查询用的参数</param>
		/// <returns>最终可以执行的 SQL 分页查询语句</returns>
		public virtual string BuildPageQuery(long skip, long take, PagingHelper.SQLParts parts, ref object[] args)
		{
			var sql = string.Format("{0}\nLIMIT @{1} OFFSET @{2}", parts.sql, args.Length, args.Length + 1);
			args = args.Concat(new object[] { take, skip }).ToArray();
			return sql;
		}

		/// <summary>
		/// 返回用于查询记录是否存在的 SQL 语句
		/// </summary>
		/// <returns>用于查询记录是否存在的 SQL 语句</returns>
		public virtual string GetExistsSql()
		{
			return "SELECT COUNT(*) FROM {0} WHERE {1}";
		}

		/// <summary>
		/// 转码表名
		/// </summary>
		/// <param name="tableName">要转码的表名</param>
		/// <returns>转码后的表名</returns>
		public virtual string EscapeTableName(string tableName)
		{
			// Assume table names with "dot" are already escaped
			return tableName.IndexOf('.') >= 0 ? tableName : EscapeSqlIdentifier(tableName);
		}

		/// <summary>
		/// 转码标识符
		/// </summary>
		/// <param name="str">要转码的表名或列名</param>
		/// <returns>转码后的表名或列名</returns>
		public virtual string EscapeSqlIdentifier(string str)
		{
			if(str[0] == '[' && str[str.Length - 1] == ']') return str;

			return string.Format("[{0}]", str);
		}

		/// <summary>
		/// 返回一个 SQL 表达式，以用来填充自增主键的字段
		/// </summary>
		/// <param name="ti">数据表信息</param>
		/// <returns>一个 SQL 表达式</returns>
		/// <remarks>参照 Oracle 数据库的相关用法</remarks>
		public virtual string GetAutoIncrementExpression(TableInfo ti)
		{
			return null;
		}

		/// <summary>
		/// 返回一个 SQL 表达式，以用来填充自增主键的返回值
		/// </summary>
		/// <param name="primaryKeyName">主键名</param>
		/// <returns>一个 SQL 表达式</returns>
		/// <remarks>参照 MS SQLServer 数据库的相关用法</remarks>
		public virtual string GetInsertOutputClause(string primaryKeyName)
		{
			return string.Empty;
		}

		/// <summary>
		/// 执行插入操作
		/// </summary>
		/// <param name="db">数据库对象</param>
		/// <param name="cmd">要执行插入的命令</param>
		/// <param name="PrimaryKeyName">主键名</param>
		/// <returns>插入后的主键值</returns>
		public virtual object ExecuteInsert(Database db, IDbCommand cmd, string PrimaryKeyName)
		{
			cmd.CommandText += ";\nSELECT @@IDENTITY AS NewID;";
			return db.ExecuteScalarHelper(cmd);
		}


		/// <summary>
		/// 返回当前正在使用的数据源类型
		/// </summary>
		/// <param name="TypeName">类型名</param>
		/// <param name="ProviderName">适配器名</param>
		/// <returns>数据源类型</returns>
		public static DatabaseType Resolve(string TypeName, string ProviderName)
		{
			// Try using type name first (more reliable)
			if(TypeName.StartsWith("MySql"))
				return Singleton<MySqlDatabaseType>.Instance;
			if(TypeName.StartsWith("SqlCe"))
				return Singleton<SqlServerCEDatabaseType>.Instance;
			if(TypeName.StartsWith("Npgsql") || TypeName.StartsWith("PgSql"))
				return Singleton<PostgreSQLDatabaseType>.Instance;
			if(TypeName.StartsWith("Oracle"))
				return Singleton<OracleDatabaseType>.Instance;
			if(TypeName.StartsWith("SQLite"))
				return Singleton<SQLiteDatabaseType>.Instance;
			if(TypeName.StartsWith("System.Data.SqlClient."))
				return Singleton<SqlServerDatabaseType>.Instance;

			// Try again with provider name
			if(ProviderName.IndexOf("MySql", StringComparison.InvariantCultureIgnoreCase) >= 0)
				return Singleton<MySqlDatabaseType>.Instance;
			if(ProviderName.IndexOf("SqlServerCe", StringComparison.InvariantCultureIgnoreCase) >= 0)
				return Singleton<SqlServerCEDatabaseType>.Instance;
			if(ProviderName.IndexOf("pgsql", StringComparison.InvariantCultureIgnoreCase) >= 0)
				return Singleton<PostgreSQLDatabaseType>.Instance;
			if(ProviderName.IndexOf("Oracle", StringComparison.InvariantCultureIgnoreCase) >= 0)
				return Singleton<OracleDatabaseType>.Instance;
			if(ProviderName.IndexOf("SQLite", StringComparison.InvariantCultureIgnoreCase) >= 0)
				return Singleton<SQLiteDatabaseType>.Instance;

			// Assume SQL Server
			return Singleton<SqlServerDatabaseType>.Instance;
		}

		#region 扩展
		/// <summary>
		/// 生成 SQL TOP 查询语句
		/// </summary>
		/// <param name="take">要获取记录数</param>
		/// <param name="dist">指定是否返回非重复记录</param>
		/// <param name="selectColumns">要获取的字段名列表</param>
		/// <param name="tableName">表名</param>
		/// <param name="joins">联合子句</param>
		/// <param name="where">条件子句</param>
		/// <param name="orderby">排序子句</param>
		/// <param name="args">SQL 查询用的参数</param>
		/// <returns>最终可以执行的 SQL 查询语句</returns>
		public virtual string BuildTopSql(int take, bool dist, string selectColumns, string tableName, string joins, string where, string orderby, List<object> args)
		{
			var sql = "SELECT {0}{1} FROM {2}{3} {4}{5} LIMIT @{6}"
				.F(
					dist ? "DISTINCT " : string.Empty,
					selectColumns,
					tableName,
					joins,
					where,
					orderby,
					args.Count
				);
			args.Add(take);
			return sql;
		}

		/// <summary>
		/// 生成 SQL 分页查询语句
		/// </summary>
		/// <param name="skip">要跳过记录数量</param>
		/// <param name="take">要获取记录数</param>
		/// <param name="dist">指定是否返回非重复记录</param>
		/// <param name="selectColumns">要获取的字段名列表</param>
		/// <param name="tableName">表名</param>
		/// <param name="joins">联合子句</param>
		/// <param name="where">条件子句</param>
		/// <param name="orderby">排序子句</param>
		/// <param name="args">SQL 查询用的参数</param>
		/// <returns>最终可以执行的 SQL 查询语句</returns>
		public virtual string BuildPagedSql(long skip, long take, bool dist, string selectColumns, string tableName, string joins, string where, string orderby, List<object> args)
		{
			var sql = "SELECT {0}{1} FROM {2}{3} {4}{5} LIMIT @{6} OFFSET @{7}"
				.F(
					dist ? "DISTINCT " : string.Empty,
					selectColumns,
					tableName,
					joins,
					where,
					orderby,
					args.Count,
					args.Count + 1
				);
			args.Add(take);
			args.Add(skip);
			return sql;
		}
		#endregion
	}
}