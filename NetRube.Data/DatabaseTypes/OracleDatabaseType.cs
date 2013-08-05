// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Data;
using NetRube.Data.Internal;


namespace NetRube.Data.DatabaseTypes
{
	/// <summary>Oracle 数据源</summary>
	class OracleDatabaseType : DatabaseType
	{
		/// <summary>获取 SQL 参数名称前缀</summary>
		/// <param name="ConnectionString">数据源连接字符串</param>
		/// <returns>参数名称前缀</returns>
		public override string GetParameterPrefix(string ConnectionString)
		{
			return ":";
		}

		/// <summary>在命令执行前对命令进行修改</summary>
		/// <param name="cmd">命令</param>
		public override void PreExecute(IDbCommand cmd)
		{
			cmd.GetType().GetProperty("BindByName").FastSetValue(cmd, true);
		}

		/// <summary>生成 SQL 分页查询语句</summary>
		/// <param name="skip">要跳过记录数量</param>
		/// <param name="take">要获取记录数</param>
		/// <param name="parts">原始 SQL 查询语句被解析后的组成部分</param>
		/// <param name="args">SQL 查询用的参数</param>
		/// <returns>最终可以执行的 SQL 分页查询语句</returns>
		/// <exception cref="System.Exception">Query must alias '*' when performing a paged query.\neg. select t.* from table t order by t.id</exception>
		public override string BuildPageQuery(long skip, long take, PagingHelper.SQLParts parts, ref object[] args)
		{
			if(parts.sqlSelectRemoved.StartsWith("*"))
				throw new Exception(Localization.Resource.PageQueryAliasException);

			// Same deal as SQL Server
			return Singleton<SqlServerDatabaseType>.Instance.BuildPageQuery(skip, take, parts, ref args);
		}

		/// <summary>转码标识符</summary>
		/// <param name="str">要转码的表名或列名</param>
		/// <returns>转码后的表名或列名</returns>
		public override string EscapeSqlIdentifier(string str)
		{
			if(str[0] == '"' && str[str.Length - 1] == '"') return str;

			return string.Format("\"{0}\"", str.ToUpperInvariant());
		}

		/// <summary>返回一个 SQL 表达式，以用来填充自增主键的字段</summary>
		/// <param name="ti">数据表信息</param>
		/// <returns>一个 SQL 表达式</returns>
		public override string GetAutoIncrementExpression(TableInfo ti)
		{
			if(!string.IsNullOrEmpty(ti.SequenceName))
				return string.Format("{0}.nextval", ti.SequenceName);

			return null;
		}

		/// <summary>执行插入操作</summary>
		/// <param name="db">数据库对象</param>
		/// <param name="cmd">要执行插入的命令</param>
		/// <param name="PrimaryKeyName">主键名</param>
		/// <returns>插入后的主键值</returns>
		public override object ExecuteInsert(Database db, IDbCommand cmd, string PrimaryKeyName)
		{
			if(PrimaryKeyName != null)
			{
				cmd.CommandText += string.Format(" returning {0} into :newid", EscapeSqlIdentifier(PrimaryKeyName));
				var param = cmd.CreateParameter();
				param.ParameterName = ":newid";
				param.Value = DBNull.Value;
				param.Direction = ParameterDirection.ReturnValue;
				param.DbType = DbType.Int64;
				cmd.Parameters.Add(param);
				db.ExecuteNonQueryHelper(cmd);
				return param.Value;
			}
			else
			{
				db.ExecuteNonQueryHelper(cmd);
				return -1;
			}
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
		public override string BuildTopSql(int take, bool dist, string selectColumns, string tableName, string joins, string where, string orderby, System.Collections.Generic.List<object> args)
		{
			var sql = "SELECT {0} FROM (SELECT {1}{0} FROM {2}{3} {4}{5}) WHERE ROWNUM <= @{6} ORDER BY ROWNUM ASC"
				.F(
					selectColumns,
					dist ? "DISTINCT " : string.Empty,
					tableName,
					joins,
					where,
					orderby,
					args.Count
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
		public override string BuildPagedSql(long skip, long take, bool dist, string selectColumns, string tableName, string joins, string where, string orderby, System.Collections.Generic.List<object> args)
		{
			if(selectColumns.StartsWith("*"))
				throw new Exception(Localization.Resource.PageQueryAliasException);

			// Same deal as SQL Server
			return Singleton<SqlServerDatabaseType>.Instance.BuildPagedSql(skip, take, dist, selectColumns, tableName, joins, where, orderby, args);
		}
		#endregion
	}
}