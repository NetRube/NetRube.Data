// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using NetRube.Data.Internal;

namespace NetRube.Data.DatabaseTypes
{
	/// <summary>
	/// SQLServer 数据源
	/// </summary>
	class SqlServerDatabaseType : DatabaseType
	{
		/// <summary>生成 SQL 分页查询语句</summary>
		/// <param name="skip">要跳过记录数量</param>
		/// <param name="take">要获取记录数</param>
		/// <param name="parts">原始 SQL 查询语句被解析后的组成部分</param>
		/// <param name="args">SQL 查询用的参数</param>
		/// <returns>最终可以执行的 SQL 分页查询语句</returns>
		public override string BuildPageQuery(long skip, long take, PagingHelper.SQLParts parts, ref object[] args)
		{
			parts.sqlSelectRemoved = PagingHelper.rxOrderBy.Replace(parts.sqlSelectRemoved, "", 1);
			if(PagingHelper.rxDistinct.IsMatch(parts.sqlSelectRemoved))
			{
				parts.sqlSelectRemoved = "peta_inner.* FROM (SELECT " + parts.sqlSelectRemoved + ") peta_inner";
			}
			var sqlPage = string.Format("SELECT * FROM (SELECT ROW_NUMBER() OVER ({0}) peta_rn, {1}) peta_paged WHERE peta_rn>@{2} AND peta_rn<=@{3}",
									parts.sqlOrderBy == null ? "ORDER BY (SELECT NULL)" : parts.sqlOrderBy, parts.sqlSelectRemoved, args.Length, args.Length + 1);
			args = args.Concat(new object[] { skip, skip + take }).ToArray();

			return sqlPage;
		}

		/// <summary>执行插入操作</summary>
		/// <param name="db">数据库对象</param>
		/// <param name="cmd">要执行插入的命令</param>
		/// <param name="PrimaryKeyName">主键名</param>
		/// <returns>插入后的主键值</returns>
		public override object ExecuteInsert(Database db, System.Data.IDbCommand cmd, string PrimaryKeyName)
		{
			return db.ExecuteScalarHelper(cmd);
		}

		/// <summary>返回用于查询记录是否存在的 SQL 语句</summary>
		/// <returns>用于查询记录是否存在的 SQL 语句</returns>
		public override string GetExistsSql()
		{
			return "IF EXISTS (SELECT 1 FROM {0} WHERE {1}) SELECT 1 ELSE SELECT 0";
		}

		/// <summary>返回一个 SQL 表达式，以用来填充自增主键的返回值</summary>
		/// <param name="primaryKeyName">主键名</param>
		/// <returns>一个 SQL 表达式</returns>
		public override string GetInsertOutputClause(string primaryKeyName)
		{
			return String.Format(" OUTPUT INSERTED.[{0}]", primaryKeyName);
		}

		#region 扩展
		/// <summary>生成 SQL TOP 查询语句</summary>
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

		/// <summary>生成 SQL 分页查询语句</summary>
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
			var sql = "SELECT {0} FROM (SELECT ROW_NUMBER() OVER ({1}) __RowNum, {0} FROM {2}{3} {4}) __Paged WHERE __RowNum > @{5} AND __RowNum <= @{6}"
				.F(
					selectColumns,
					orderby.IsNullOrEmpty_() ? "ORDER BY (SELECT NULL)" : orderby,
					tableName,
					joins,
					where,
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