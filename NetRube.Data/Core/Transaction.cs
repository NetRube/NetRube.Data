// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: Transaction.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

using System;

/// <summary>
/// Data 命名空间
/// </summary>
namespace NetRube.Data
{
	/// <summary>
	/// 事务接口
	/// </summary>
	public interface ITransaction : IDisposable
	{
		/// <summary>
		/// 完成事务
		/// </summary>
		void Complete();
	}

	/// <summary>
	/// 事务
	/// </summary>
	public class Transaction : ITransaction
	{
		/// <summary>
		/// 初始化一个新 <see cref="Transaction" /> 实例。
		/// </summary>
		/// <param name="db">要开始事务的数据源实例</param>
		public Transaction(Database db)
		{
			_db = db;
			_db.BeginTransaction();
		}

		/// <summary>
		/// 完成事务
		/// </summary>
		public void Complete()
		{
			_db.CompleteTransaction();
			_db = null;
		}

		/// <summary>
		/// 释放事务
		/// </summary>
		public void Dispose()
		{
			if(_db != null)
				_db.AbortTransaction();
		}

		Database _db;
	}
}