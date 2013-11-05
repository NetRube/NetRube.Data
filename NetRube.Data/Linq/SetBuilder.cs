// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: SetBuilder.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

/// <summary>
/// Data 命名空间
/// </summary>
namespace NetRube.Data
{
	/// <summary>
	/// 更新构建器
	/// </summary>
	/// <typeparam name="T">实体类型</typeparam>
	public class SetBuilder<T> where T : new()
	{
		private Database __db;
		private UpdateBuilder<T> __set;

		/// <summary>
		/// 初始化一个新 <see cref="SetBuilder&lt;T&gt;" /> 实例。
		/// </summary>
		/// <param name="db">数据库实例</param>
		public SetBuilder(Database db)
		{
			__db = db;
			this.Params = new List<object>();
			__set = new UpdateBuilder<T>(db, this.Params);
		}

		/// <summary>
		/// 获取参数
		/// </summary>
		/// <value>参数集合</value>
		public List<object> Params { get; private set; }

		#region Set
		/// <summary>
		/// 要更新的字段
		/// </summary>
		/// <param name="property">实体属性表达式</param>
		/// <param name="value">值</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Set(Expression<Func<T, object>> property, object value)
		{
			__set.Append(property, value);

			return this;
		}

		/// <summary>
		/// 要更新的字段
		/// </summary>
		/// <param name="property">实体属性表达式</param>
		/// <param name="expression">值</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Set(Expression<Func<T, object>> property, Expression<Func<T, object>> expression)
		{
			__set.Append(property, expression);

			return this;
		}

		/// <summary>
		/// 要更新的实体
		/// </summary>
		/// <param name="entity">要更新的实体</param>
		/// <param name="refer">用于参照的实体</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Set(T entity, T refer)
		{
			return Set(TrackingEntity<T>.GetChanges(entity, refer));
		}

		/// <summary>
		/// 要更新的实体
		/// </summary>
		/// <param name="snapshot">实体快照</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Set(TrackingEntity<T> snapshot)
		{
			return Set(snapshot.GetChanges());
		}

		/// <summary>
		/// 要更新的实体
		/// </summary>
		/// <param name="changes">被更改属性列表</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Set(List<TrackingEntity<T>.Change> changes)
		{
			changes.ForEach(p => __set.Append(p.Name, p.NewValue));

			return this;
		}
		#endregion

		private WhereBuilder __where;
		#region WHERE
		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="expression">表达式</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Where(Expression expression)
		{
			if(__where == null)
				__where = new WhereBuilder(__db, this.Params, false);
			__where.Append(expression);

			return this;
		}

		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="expression">表达式</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Where(Expression<Func<T, bool>> expression)
		{
			return this.Where((Expression)expression);
		}

		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="property">字段栏表达式</param>
		/// <param name="op">比较运算符</param>
		/// <param name="value">值</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Where(Expression property, QueryOperatorType op, object value)
		{
			if(__where == null)
				__where = new WhereBuilder(__db, this.Params, false);
			__where.Append(property, op, value);

			return this;
		}

		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="property">字段栏表达式</param>
		/// <param name="op">比较运算符</param>
		/// <param name="value">值</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> Where(Expression<Func<T, object>> property, QueryOperatorType op, object value)
		{
			return this.Where((Expression)property, op, value);
		}
		#endregion

		#region WHERE OR
		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="expression">表达式</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> WhereOr(Expression expression)
		{
			if(__where == null)
				__where = new WhereBuilder(__db, this.Params, false);
			__where.AppendOr(expression);

			return this;
		}

		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="expression">表达式</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> WhereOr(Expression<Func<T, bool>> expression)
		{
			return this.WhereOr((Expression)expression);
		}

		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="property">字段栏表达式</param>
		/// <param name="op">比较运算符</param>
		/// <param name="value">值</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> WhereOr(Expression property, QueryOperatorType op, object value)
		{
			if(__where == null)
				__where = new WhereBuilder(__db, this.Params, false);
			__where.AppendOr(property, op, value);

			return this;
		}

		/// <summary>
		/// 查询条件
		/// </summary>
		/// <param name="property">字段栏表达式</param>
		/// <param name="op">比较运算符</param>
		/// <param name="value">值</param>
		/// <returns>更新构建器</returns>
		public SetBuilder<T> WhereOr(Expression<Func<T, object>> property, QueryOperatorType op, object value)
		{
			return this.WhereOr((Expression)property, op, value);
		}
		#endregion

		#region 内部操作
		private string GetWhere(bool prefix = true)
		{
			if(__where.IsNull_()) return string.Empty;
			if(prefix)
				return "WHERE " + __where.ToString();
			return __where.ToString();
		}
		private object[] GetParams()
		{
			return this.Params.ToArray();
		}
		#endregion

		#region 执行
		/// <summary>
		/// 执行操作
		/// </summary>
		/// <returns>受影响的行数</returns>
		public int Execute()
		{
			var sets = __set.ToString();
			if(sets.IsNullOrEmpty_()) return 0;
			var sql = "UPDATE {0} SET {1} {2}".F(__db.GetTableName<T>(), sets, this.GetWhere());
			return __db.Execute(sql, this.GetParams());
		}

		/// <summary>
		/// 返回是否执行成功
		/// </summary>
		/// <returns>指示是否执行成功</returns>
		public bool Succeed()
		{
			return this.Execute() > 0;
		}
		#endregion
	}
}