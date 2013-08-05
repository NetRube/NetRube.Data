using System;
using System.Linq.Expressions;

namespace NetRube.Data
{
	/// <summary>表达式处理器</summary>
	internal class ExpressionVisitor
	{
		/// <summary>处理表达式或委托</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">expression</exception>
		protected virtual Expression Visit(Expression expression)
		{
			if(expression == null)
				return null;

			switch(expression.NodeType)
			{
				case ExpressionType.Lambda:
					return VisitLamda((LambdaExpression)expression);
				case ExpressionType.ArrayLength:
				case ExpressionType.Convert:
				case ExpressionType.ConvertChecked:
				case ExpressionType.Negate:
				case ExpressionType.UnaryPlus:
				case ExpressionType.NegateChecked:
				case ExpressionType.Not:
				case ExpressionType.Quote:
				case ExpressionType.TypeAs:
					return this.VisitUnary((UnaryExpression)expression);
				case ExpressionType.Add:
				case ExpressionType.AddChecked:
				case ExpressionType.And:
				case ExpressionType.AndAlso:
				case ExpressionType.ArrayIndex:
				case ExpressionType.Coalesce:
				case ExpressionType.Divide:
				case ExpressionType.Equal:
				case ExpressionType.ExclusiveOr:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case ExpressionType.LeftShift:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
				case ExpressionType.Modulo:
				case ExpressionType.Multiply:
				case ExpressionType.MultiplyChecked:
				case ExpressionType.NotEqual:
				case ExpressionType.Or:
				case ExpressionType.OrElse:
				case ExpressionType.Power:
				case ExpressionType.RightShift:
				case ExpressionType.Subtract:
				case ExpressionType.SubtractChecked:
					return this.VisitBinary((BinaryExpression)expression);
				case ExpressionType.Call:
					return this.VisitMethodCall((MethodCallExpression)expression);
				case ExpressionType.Constant:
					return this.VisitConstant((ConstantExpression)expression);
				case ExpressionType.MemberAccess:
					return this.VisitMemberAccess((MemberExpression)expression);
				case ExpressionType.Parameter:
					return this.VisitParameter((ParameterExpression)expression);
				case ExpressionType.New:
					return this.VisitNew((NewExpression)expression);
			}
			throw new ArgumentOutOfRangeException("expression", expression.NodeType.ToString());
		}

		/// <summary>处理常量表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitConstant(ConstantExpression expression)
		{
			return expression;
		}

		/// <summary>处理字段或属性表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitMemberAccess(MemberExpression expression)
		{
			return expression;
		}

		/// <summary>处理方法调用表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitMethodCall(MethodCallExpression expression)
		{
			return expression;
		}

		/// <summary>处理二元运算表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitBinary(BinaryExpression expression)
		{
			this.Visit(expression.Left);
			this.Visit(expression.Right);
			return expression;
		}

		/// <summary>处理一元运算表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitUnary(UnaryExpression expression)
		{
			this.Visit(expression.Operand);
			return expression;
		}

		/// <summary>处理 Lambda 表达式</summary>
		/// <param name="lambdaExpression">Lambda 表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitLamda(LambdaExpression lambdaExpression)
		{
			this.Visit(lambdaExpression.Body);
			return lambdaExpression;
		}

		/// <summary>处理参数表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		private Expression VisitParameter(ParameterExpression expression)
		{
			return expression;
		}

		/// <summary>处理构造函数调用表达式</summary>
		/// <param name="expression">表达式</param>
		/// <returns>表达式</returns>
		protected virtual Expression VisitNew(NewExpression expression)
		{
			return expression;
		}

		/// <summary>获取二元表达式右则的值</summary>
		/// <param name="expression">表达式</param>
		/// <returns>二元表达式右则计算出来的值</returns>
		protected virtual object GetRightValue(Expression expression)
		{
			if(expression.NodeType == ExpressionType.Constant)
				return ((ConstantExpression)expression).Value;
			//return Expression.Lambda(expression, new ParameterExpression[0]).Compile().DynamicInvoke(new object[0]);
			return Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object)), new ParameterExpression[0]).Compile()();
		}

		/// <summary>获取查询操作符</summary>
		/// <param name="op">操作符类型</param>
		/// <returns>查询操作符</returns>
		/// <exception cref="System.NotSupportedException">指定操作符未实现</exception>
		protected virtual string GetOperator(QueryOperatorType op)
		{
			switch(op)
			{
				case QueryOperatorType.Equal:
					return " = ";
				case QueryOperatorType.NotEqual:
					return " <> ";
				case QueryOperatorType.GreaterThan:
					return " > ";
				case QueryOperatorType.LessThan:
					return " < ";
				case QueryOperatorType.GreaterThanOrEqual:
					return " >= ";
				case QueryOperatorType.LessThanOrEqual:
					return " <= ";
				case QueryOperatorType.Contains:
				case QueryOperatorType.StartsWith:
				case QueryOperatorType.EndsWith:
					return " LIKE ";
				default:
					throw new NotSupportedException(Localization.Resource.OperatorNotSupportedException.F(op));
			}
		}

		/// <summary>获取查询操作符</summary>
		/// <param name="expType">表达式树节点类型</param>
		/// <returns>查询操作符</returns>
		/// <exception cref="System.NotSupportedException">指定操作符未实现</exception>
		protected virtual string GetOperator(ExpressionType expType)
		{
			switch(expType)
			{
				case ExpressionType.AndAlso:
				case ExpressionType.And:
					return " AND ";
				case ExpressionType.Equal:
					return " = ";
				case ExpressionType.GreaterThan:
					return " > ";
				case ExpressionType.GreaterThanOrEqual:
					return " >= ";
				case ExpressionType.LessThan:
					return " < ";
				case ExpressionType.LessThanOrEqual:
					return " <= ";
				case ExpressionType.NotEqual:
					return " <> ";
				case ExpressionType.OrElse:
				case ExpressionType.Or:
					return " OR ";
				case ExpressionType.Add:
					return " + ";
				case ExpressionType.Divide:
					return " / ";
				case ExpressionType.Modulo:
					return " MOD ";
				case ExpressionType.Multiply:
					return " * ";
				case ExpressionType.Power:
					return " + ";
				case ExpressionType.Subtract:
					return " - ";
				default:
					throw new NotSupportedException(Localization.Resource.OperatorNotSupportedException.F(expType));
			}
		}
	}
}