// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: Sql.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetRube.Data.Internal;

/// <summary>
/// Data 命名空间
/// </summary>
namespace NetRube.Data
{
	/// <summary>
	/// 简单的 SQL 语句生成器
	/// </summary>
	public class Sql
	{
		/// <summary>
		/// 初始化一个新 <see cref="Sql" /> 实例。
		/// </summary>
		public Sql()
		{
		}

		/// <summary>
		/// 初始化一个新 <see cref="Sql" /> 实例。
		/// </summary>
		/// <param name="sql">SQL 语句或片段</param>
		/// <param name="args">SQL 所用的参数</param>
		public Sql(string sql, params object[] args)
		{
			_sql = sql;
			_args = args;
		}

		/// <summary>
		/// 实例化一个新的 SQL 语句生成器
		/// </summary>
		/// <value>SQL 语句生成器</value>
		public static Sql Builder
		{
			get { return new Sql(); }
		}

		string _sql;
		object[] _args;
		Sql _rhs;
		string _sqlFinal;
		object[] _argsFinal;

		private void Build()
		{
			// already built?
			if(_sqlFinal != null)
				return;

			// Build it
			var sb = new StringBuilder();
			var args = new List<object>();
			Build(sb, args, null);
			_sqlFinal = sb.ToString();
			_argsFinal = args.ToArray();
		}

		/// <summary>
		/// 返回最终生成的 SQL 语句
		/// </summary>
		/// <value>SQL 语句</value>
		public string SQL
		{
			get
			{
				Build();
				return _sqlFinal;
			}
		}

		/// <summary>
		/// 获取最终生成后的参数
		/// </summary>
		/// <value>参数</value>
		public object[] Arguments
		{
			get
			{
				Build();
				return _argsFinal;
			}
		}

		/// <summary>
		/// 添加另一个 SQL 生成器到当前的 SQL 生成器
		/// </summary>
		/// <param name="sql">要添加的 SQL 生成器</param>
		/// <returns>合并后的 SQL 生成器</returns>
		public Sql Append(Sql sql)
		{
			if(_rhs != null)
				_rhs.Append(sql);
			else
				_rhs = sql;

			return this;
		}

		/// <summary>
		/// 添加一个 SQL 语句或片段
		/// </summary>
		/// <param name="sql">要添加的 SQL 语句或片段</param>
		/// <param name="args">SQL 语句或片段所用的参数</param>
		/// <returns>当前的 SQL 生成器</returns>
		public Sql Append(string sql, params object[] args)
		{
			return Append(new Sql(sql, args));
		}

		static bool Is(Sql sql, string sqltype)
		{
			return sql != null && sql._sql != null && sql._sql.StartsWith(sqltype, StringComparison.InvariantCultureIgnoreCase);
		}

		private void Build(StringBuilder sb, List<object> args, Sql lhs)
		{
			if(!String.IsNullOrEmpty(_sql))
			{
				// Add SQL to the string
				if(sb.Length > 0)
				{
					sb.Append("\n");
				}

				var sql = ParametersHelper.ProcessParams(_sql, _args, args);

				if(Is(lhs, "WHERE ") && Is(this, "WHERE "))
					sql = "AND " + sql.Substring(6);
				if(Is(lhs, "ORDER BY ") && Is(this, "ORDER BY "))
					sql = ", " + sql.Substring(9);

				sb.Append(sql);
			}

			// Now do rhs
			if(_rhs != null)
				_rhs.Build(sb, args, this);
		}

		/// <summary>
		/// 添加 WHERE 子句
		/// </summary>
		/// <param name="sql">要添加的 WHERE 子句</param>
		/// <param name="args">WHERE 子句所用的参数</param>
		/// <returns>当前的 SQL 生成器</returns>
		public Sql Where(string sql, params object[] args)
		{
			return Append(new Sql("WHERE (" + sql + ")", args));
		}

		/// <summary>
		/// 添加 ORDER BY 子句
		/// </summary>
		/// <param name="columns">要添加的 ORDER BY 子句</param>
		/// <returns>当前的 SQL 生成器</returns>
		public Sql OrderBy(params object[] columns)
		{
			return Append(new Sql("ORDER BY " + String.Join(", ", (from x in columns select x.ToString()).ToArray())));
		}

		/// <summary>
		/// 添加 SELECT 子句
		/// </summary>
		/// <param name="columns">要添加的 SELECT 子句所用的字段名列表</param>
		/// <returns>当前的 SQL 生成器</returns>
		public Sql Select(params object[] columns)
		{
			return Append(new Sql("SELECT " + String.Join(", ", (from x in columns select x.ToString()).ToArray())));
		}

		/// <summary>
		/// 添加 FROM 子句
		/// </summary>
		/// <param name="tables">要添加的 FROM 子句所用的表名列表</param>
		/// <returns>当前的 SQL 生成器</returns>
		public Sql From(params object[] tables)
		{
			return Append(new Sql("FROM " + String.Join(", ", (from x in tables select x.ToString()).ToArray())));
		}

		/// <summary>
		/// 添加 GROUP BY 子句
		/// </summary>
		/// <param name="columns">要添加的 GROUP BY 子句所用的字段名列表</param>
		/// <returns>当前的 SQL 生成器</returns>
		public Sql GroupBy(params object[] columns)
		{
			return Append(new Sql("GROUP BY " + String.Join(", ", (from x in columns select x.ToString()).ToArray())));
		}

		private SqlJoinClause Join(string JoinType, string table)
		{
			return new SqlJoinClause(Append(new Sql(JoinType + table)));
		}

		/// <summary>
		/// 添加 INNER JOIN 子句
		/// </summary>
		/// <param name="table">要添加的 INNER JOIN 子句所用的表名</param>
		/// <returns>当前的 SQL 生成器</returns>
		public SqlJoinClause InnerJoin(string table) { return Join("INNER JOIN ", table); }

		/// <summary>
		/// 添加 LEFT JOIN 子句
		/// </summary>
		/// <param name="table">要添加的 LEFT JOIN 子句所用的表名</param>
		/// <returns>当前的 SQL 生成器</returns>
		public SqlJoinClause LeftJoin(string table) { return Join("LEFT JOIN ", table); }

		/// <summary>
		/// 用以 SQL 生成器的简单的 JOIN 语句生成器
		/// </summary>
		public class SqlJoinClause
		{
			private readonly Sql _sql;

			/// <summary>
			/// 初始化一个新 <see cref="SqlJoinClause" /> 实例。
			/// </summary>
			/// <param name="sql">SQL 生成器</param>
			public SqlJoinClause(Sql sql)
			{
				_sql = sql;
			}

			/// <summary>
			/// 为 JOIN 语句添加 ON 子句
			/// </summary>
			/// <param name="onClause">要添加的 ON 子句</param>
			/// <param name="args">要添加的 ON 子句所用的参数</param>
			/// <returns>当前的 SQL 生成器</returns>
			public Sql On(string onClause, params object[] args)
			{
				return _sql.Append("ON " + onClause, args);
			}
		}
	}
}