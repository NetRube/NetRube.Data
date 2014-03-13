// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: SqlServerCEDatabaseType.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2014-03-13
// ***********************************************************************

// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using NetRube.Data.Internal;

/// <summary>
/// DatabaseTypes 命名空间
/// </summary>
namespace NetRube.Data.DatabaseTypes
{
	/// <summary>
	/// SqlServer CE 数据源
	/// </summary>
	class SqlServerCEDatabaseType : DatabaseType
	{
		/// <summary>
		/// 生成 SQL 分页查询语句
		/// </summary>
		/// <param name="skip">要跳过记录数量</param>
		/// <param name="take">要获取记录数</param>
		/// <param name="parts">原始 SQL 查询语句被解析后的组成部分</param>
		/// <param name="args">SQL 查询用的参数</param>
		/// <returns>最终可以执行的 SQL 分页查询语句</returns>
		public override string BuildPageQuery(long skip, long take, PagingHelper.SQLParts parts, ref object[] args)
		{
			var sqlPage = string.Format("{0}\nOFFSET @{1} ROWS FETCH NEXT @{2} ROWS ONLY", parts.sql, args.Length, args.Length + 1);
			args = args.Concat(new object[] { skip, take }).ToArray();
			return sqlPage;
		}

		/// <summary>
		/// 执行插入操作
		/// </summary>
		/// <param name="db">数据库对象</param>
		/// <param name="cmd">要执行插入的命令</param>
		/// <param name="PrimaryKeyName">主键名</param>
		/// <returns>插入后的主键值</returns>
		public override object ExecuteInsert(Database db, System.Data.IDbCommand cmd, string PrimaryKeyName)
		{
			db.ExecuteNonQueryHelper(cmd);
			return db.ExecuteScalar<object>("SELECT @@@IDENTITY AS NewID;");
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
		public override string BuildTopSql(int take, bool dist, string selectColumns, string tableName, string joins, string where, string orderby, List<object> args)
		{
			var sql = "SELECT {0}TOP (@{1}) {2} FROM {3}{4} {5}{6}"
				.F(
					dist ? "DISTINCT " : string.Empty,
					args.Count,
					selectColumns,
					tableName,
					joins,
					where,
					orderby
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
		public override string BuildPagedSql(long skip, long take, bool dist, string selectColumns, string tableName, string joins, string where, string orderby, List<object> args)
		{
			var sql = "SELECT {0}{1} FROM {2}{3} {4}{5}\nOFFSET @{6} ROWS FETCH NEXT @{7} ROWS ONLY"
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
			args.Add(skip);
			args.Add(take);
			return sql;
		}
		#endregion
	}
}