/* PetaPoco - A Tiny ORMish thing for your POCO's.
 * Copyright © 2011-2012 Topten Software.  All Rights Reserved.
 * 
 * Apache License 2.0 - http://www.toptensoftware.com/petapoco/license
 * 
 * Special thanks to Rob Conery (@robconery) for original inspiration (ie:Massive) and for 
 * use of Subsonic's T4 templates, Rob Sullivan (@DataChomp) for hard core DBA advice 
 * and Adam Schroder (@schotime) for lots of suggestions, improvements and Oracle support
 */

// Define PETAPOCO_NO_DYNAMIC in your project settings on .NET 3.5

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NetRube.Data.Internal;


namespace NetRube.Data
{
	/// <summary>数据源操作主类</summary>
	public class Database : IDisposable
	{
		#region Constructors
		/// <summary>使用指定的数据源连接初始化一个新 <see cref="Database"/> 实例。</summary>
		/// <param name="connection">数据源连接</param>
		/// <remarks>
		/// The supplied IDbConnection will not be closed/disposed by PetaPoco - that remains
		/// the responsibility of the caller.
		/// </remarks>
		public Database(IDbConnection connection)
		{
			_sharedConnection = connection;
			_connectionString = connection.ConnectionString;
			_sharedConnectionDepth = 2;		// Prevent closing external connection
			CommonConstruct();
		}

		/// <summary>使用指定的数据源连接串和适配器名初始化一个新 <see cref="Database" /> 实例。</summary>
		/// <param name="connectionString">数据源连接串</param>
		/// <param name="providerName">适配器名</param>
		/// <remarks>
		/// PetaPoco will automatically close and dispose any connections it creates.
		/// </remarks>
		public Database(string connectionString, string providerName)
		{
			_connectionString = connectionString;
			_providerName = providerName;
			CommonConstruct();
		}

		/// <summary>使用指定的数据源连接串和适配器初始化一个新 <see cref="Database" /> 实例。</summary>
		/// <param name="connectionString">数据源连接串</param>
		/// <param name="provider">适配器</param>
		public Database(string connectionString, DbProviderFactory provider)
		{
			_connectionString = connectionString;
			_factory = provider;
			CommonConstruct();
		}

		/// <summary>使用配置文件中指定的数据源连接名初始化一个新 <see cref="Database" /> 实例。</summary>
		/// <param name="connectionStringName">配置文件中的数据源连接名</param>
		/// <exception cref="System.InvalidOperationException">配置文件中的数据源连接名无效</exception>
		public Database(string connectionStringName)
		{
			// Use first?
			if(connectionStringName == "")
				connectionStringName = ConfigurationManager.ConnectionStrings[0].Name;

			// Work out connection string and provider name
			var providerName = "System.Data.SqlClient";
			if(ConfigurationManager.ConnectionStrings[connectionStringName] != null)
			{
				if(!string.IsNullOrEmpty(ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName))
					providerName = ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName;
			}
			else
			{
				throw new InvalidOperationException(Localization.Resource.InvalidConnectionStringSettingsException.F(connectionStringName));
			}

			// Store factory and connection string
			_connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
			_providerName = providerName;
			CommonConstruct();
		}

		/// <summary>
		/// Provides common initialization for the various constructors
		/// </summary>
		private void CommonConstruct()
		{
			// Reset
			_transactionDepth = 0;
			EnableAutoSelect = true;
			EnableNamedParams = true;

			// If a provider name was supplied, get the IDbProviderFactory for it
			if(_providerName != null)
				_factory = DbProviderFactories.GetFactory(_providerName);

			// Resolve the DB Type
			string DBTypeName = (_factory == null ? _sharedConnection.GetType() : _factory.GetType()).Name;
			_dbType = DatabaseType.Resolve(DBTypeName, _providerName);

			// What character is used for delimiting parameters in SQL
			_paramPrefix = _dbType.GetParameterPrefix(_connectionString);
		}

		#endregion

		#region IDisposable
		/// <summary>自动关闭打开的共享连接</summary>
		public void Dispose()
		{
			// Automatically close one open connection reference
			//  (Works with KeepConnectionAlive and manually opening a shared connection)
			CloseSharedConnection();
		}
		#endregion

		#region Connection Management
		/// <summary>获取或设置是否保持连接。当为 <c>true</c> 时，第一次打开的连接将持续到此数据源对象销毁时。</summary>
		/// <value>如果保持连接，则该值为 <c>true</c>；否则为 <c>false</c>。</value>
		public bool KeepConnectionAlive
		{
			get;
			set;
		}

		/// <summary>打开共享连接</summary>
		/// <remarks>
		/// Calls to Open/CloseSharedConnection are reference counted and should be balanced
		/// </remarks>
		public void OpenSharedConnection()
		{
			if(_sharedConnectionDepth == 0)
			{
				_sharedConnection = _factory.CreateConnection();
				_sharedConnection.ConnectionString = _connectionString;

				if(_sharedConnection.State == ConnectionState.Broken)
					_sharedConnection.Close();

				if(_sharedConnection.State == ConnectionState.Closed)
					_sharedConnection.Open();

				_sharedConnection = OnConnectionOpened(_sharedConnection);

				if(KeepConnectionAlive)
					_sharedConnectionDepth++;		// Make sure you call Dispose
			}
			_sharedConnectionDepth++;
		}

		/// <summary>释放共享连接</summary>
		public void CloseSharedConnection()
		{
			if(_sharedConnectionDepth > 0)
			{
				_sharedConnectionDepth--;
				if(_sharedConnectionDepth == 0)
				{
					OnConnectionClosing(_sharedConnection);
					_sharedConnection.Dispose();
					_sharedConnection = null;
				}
			}
		}

		/// <summary>获取当前的共享连接，如果没有连接将返回 null</summary>
		/// <value>The connection.</value>
		public IDbConnection Connection
		{
			get { return _sharedConnection; }
		}

		#endregion

		#region Transaction Management
		// Helper to create a transaction scope

		/// <summary>开始或继续事务</summary>
		/// <returns>一个 <see cref="ITransaction" /> 引用</returns>
		/// <remarks>
		/// This method makes management of calls to Begin/End/CompleteTransaction easier.
		/// The usage pattern for this should be:
		/// using (var tx = db.GetTransaction())
		/// {
		/// Do stuff
		/// db.Update(...);
		/// Mark the transaction as complete
		/// tx.Complete();
		/// }
		/// Transactions can be nested but they must all be completed otherwise the entire
		/// transaction is aborted.
		/// </remarks>
		public ITransaction GetTransaction()
		{
			return new Transaction(this);
		}

		/// <summary>在开始事务时调用。</summary>
		public virtual void OnBeginTransaction()
		{
		}

		/// <summary>在结束事务时调用。</summary>
		public virtual void OnEndTransaction()
		{
		}

		/// <summary>开始事务</summary>
		public void BeginTransaction()
		{
			_transactionDepth++;

			if(_transactionDepth == 1)
			{
				OpenSharedConnection();
				_transaction = _sharedConnection.BeginTransaction();
				_transactionCancelled = false;
				OnBeginTransaction();
			}

		}

		/// <summary>
		/// Internal helper to cleanup transaction
		/// </summary>
		void CleanupTransaction()
		{
			OnEndTransaction();

			if(_transactionCancelled)
				_transaction.Rollback();
			else
				_transaction.Commit();

			_transaction.Dispose();
			_transaction = null;

			CloseSharedConnection();
		}

		/// <summary>中止事务</summary>
		/// <remarks>
		/// Called automatically by Transaction.Dispose()
		/// if the transaction wasn't completed.
		/// </remarks>
		public void AbortTransaction()
		{
			_transactionCancelled = true;
			if((--_transactionDepth) == 0)
				CleanupTransaction();
		}

		/// <summary>完成事务</summary>
		public void CompleteTransaction()
		{
			if((--_transactionDepth) == 0)
				CleanupTransaction();
		}

		#endregion

		#region Command Management
		/// <summary>
		/// Add a parameter to a DB command
		/// </summary>
		/// <param name="cmd">A reference to the IDbCommand to which the parameter is to be added</param>
		/// <param name="value">The value to assign to the parameter</param>
		/// <param name="pi">Optional, a reference to the property info of the POCO property from which the value is coming.</param>
		void AddParam(IDbCommand cmd, object value, PropertyInfo pi)
		{
			// Convert value to from poco type to db type
			if(pi != null)
			{
				var mapper = Mappers.GetMapper(pi.DeclaringType);
				var fn = mapper.GetToDbConverter(pi);
				if(fn != null)
					value = fn(value);
			}

			// Support passed in parameters
			var idbParam = value as IDbDataParameter;
			if(idbParam != null)
			{
				idbParam.ParameterName = string.Format("{0}{1}", _paramPrefix, cmd.Parameters.Count);
				cmd.Parameters.Add(idbParam);
				return;
			}

			// Create the parameter
			var p = cmd.CreateParameter();
			p.ParameterName = string.Format("{0}{1}", _paramPrefix, cmd.Parameters.Count);

			// Assign the parmeter value
			if(value == null)
			{
				p.Value = DBNull.Value;
			}
			else
			{
				// Give the database type first crack at converting to DB required type
				value = _dbType.MapParameterValue(value);

				var t = value.GetType();
				if(t.IsEnum)		// PostgreSQL .NET driver wont cast enum to int
				{
					p.Value = (int)value;
				}
				else if(t == typeof(Guid))
				{
					p.Value = value.ToString();
					p.DbType = DbType.String;
					p.Size = 40;
				}
				else if(t == typeof(string))
				{
					// out of memory exception occurs if trying to save more than 4000 characters to SQL Server CE NText column. Set before attempting to set Size, or Size will always max out at 4000
					if((value as string).Length + 1 > 4000 && p.GetType().Name == "SqlCeParameter")
						p.GetType().GetProperty("SqlDbType").SetValue(p, SqlDbType.NText, null);

					p.Size = Math.Max((value as string).Length + 1, 4000);		// Help query plan caching by using common size
					p.Value = value;
				}
				else if(t == typeof(AnsiString))
				{
					// Thanks @DataChomp for pointing out the SQL Server indexing performance hit of using wrong string type on varchar
					p.Size = Math.Max((value as AnsiString).Value.Length + 1, 4000);
					p.Value = (value as AnsiString).Value;
					p.DbType = DbType.AnsiString;
				}
				else if(value.GetType().Name == "SqlGeography") //SqlGeography is a CLR Type
				{
					p.GetType().GetProperty("UdtTypeName").SetValue(p, "geography", null); //geography is the equivalent SQL Server Type
					p.Value = value;
				}

				else if(value.GetType().Name == "SqlGeometry") //SqlGeometry is a CLR Type
				{
					p.GetType().GetProperty("UdtTypeName").SetValue(p, "geometry", null); //geography is the equivalent SQL Server Type
					p.Value = value;
				}
				else
				{
					p.Value = value;
				}
			}

			// Add to the collection
			cmd.Parameters.Add(p);
		}

		// Create a command
		static Regex rxParamsPrefix = new Regex(@"(?<!@)@\w+", RegexOptions.Compiled);
		/// <summary>创建命令</summary>
		/// <param name="connection">数据源连接</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>命令</returns>
		public IDbCommand CreateCommand(IDbConnection connection, string sql, params object[] args)
		{
			// Perform named argument replacements
			if(EnableNamedParams)
			{
				var new_args = new List<object>();
				sql = ParametersHelper.ProcessParams(sql, args, new_args);
				args = new_args.ToArray();
			}

			// Perform parameter prefix replacements
			if(_paramPrefix != "@")
				sql = rxParamsPrefix.Replace(sql, m => _paramPrefix + m.Value.Substring(1));
			sql = sql.Replace("@@", "@");		   // <- double @@ escapes a single @

			// Create the command and add parameters
			IDbCommand cmd = connection.CreateCommand();
			cmd.Connection = connection;
			cmd.CommandText = sql;
			cmd.Transaction = _transaction;
			foreach(var item in args)
			{
				AddParam(cmd, item, null);
			}

			// Notify the DB type
			_dbType.PreExecute(cmd);

			// Call logging
			if(!String.IsNullOrEmpty(sql))
				DoPreExecute(cmd);

			return cmd;
		}
		#endregion

		#region Exception Reporting and Logging

		/// <summary>在数据源操作过程出现异常时调用，可以在派生类中重新实现日志记录。</summary>
		/// <param name="x">异常信息</param>
		/// <returns>如果为 <c>true</c> 表示重新引发异常，如果为 <c>false</c> 表示阻止异常</returns>
		public virtual bool OnException(Exception x)
		{
			System.Diagnostics.Debug.WriteLine(x.ToString());
			System.Diagnostics.Debug.WriteLine(LastCommand);
			return true;
		}

		/// <summary>在打开连接时调用。</summary>
		/// <param name="conn">数据源连接</param>
		/// <returns>原来或新的数据源连接</returns>
		/// <remarks>
		/// Override this method to provide custom logging of opening connection, or
		/// to provide a proxy IDbConnection.
		/// </remarks>
		public virtual IDbConnection OnConnectionOpened(IDbConnection conn)
		{
			return conn;
		}

		/// <summary>在关闭连接时调用。</summary>
		/// <param name="conn">数据源连接</param>
		public virtual void OnConnectionClosing(IDbConnection conn)
		{
		}

		/// <summary>在命令执行前时调用。</summary>
		/// <param name="cmd">命令</param>
		/// <remarks>
		/// Override this method to provide custom logging of commands and/or
		/// modification of the IDbCommand before it's executed
		/// </remarks>
		public virtual void OnExecutingCommand(IDbCommand cmd)
		{
		}

		/// <summary>在命令执行完成时调用。</summary>
		/// <param name="cmd">命令</param>
		public virtual void OnExecutedCommand(IDbCommand cmd)
		{
		}

		#endregion

		#region operation: Execute
		/// <summary>执行指定的 SQL 语句</summary>
		/// <param name="sql">要执行的 SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>受影响的行数</returns>
		public int Execute(string sql, params object[] args)
		{
			try
			{
				OpenSharedConnection();
				try
				{
					using(var cmd = CreateCommand(_sharedConnection, sql, args))
					{
						var retv = cmd.ExecuteNonQuery();
						OnExecutedCommand(cmd);
						return retv;
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch(Exception x)
			{
				if(OnException(x))
					throw;
				return -1;
			}
		}

		/// <summary>执行指定的 SQL 语句</summary>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>受影响的行数</returns>
		public int Execute(Sql sql)
		{
			return Execute(sql.SQL, sql.Arguments);
		}

		#endregion

		#region operation: ExecuteScalar

		/// <summary>执行操作命令，并返回结果集中的第一行的第一列的值</summary>
		/// <typeparam name="T">返回的数据类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集中的第一行的第一列的值</returns>
		public T ExecuteScalar<T>(string sql, params object[] args)
		{
			try
			{
				OpenSharedConnection();
				try
				{
					using(var cmd = CreateCommand(_sharedConnection, sql, args))
					{
						object val = cmd.ExecuteScalar();
						OnExecutedCommand(cmd);

						// Handle nullable types
						Type u = Nullable.GetUnderlyingType(typeof(T));
						if(u != null && val == null)
							return default(T);

						return (T)Convert.ChangeType(val, u == null ? typeof(T) : u);
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch(Exception x)
			{
				if(OnException(x))
					throw;
				return default(T);
			}
		}

		/// <summary>执行操作命令，并返回结果集中的第一行的第一列的值</summary>
		/// <typeparam name="T">返回的数据类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集中的第一行的第一列的值</returns>
		public T ExecuteScalar<T>(Sql sql)
		{
			return ExecuteScalar<T>(sql.SQL, sql.Arguments);
		}

		#endregion

		#region operation: Fetch

		/// <summary>获取查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>查询结果</returns>
		public List<T> Fetch<T>(string sql, params object[] args)
		{
			return Query<T>(sql, args).ToList();
		}

		/// <summary>获取查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>查询结果</returns>
		public List<T> Fetch<T>(Sql sql)
		{
			return Fetch<T>(sql.SQL, sql.Arguments);
		}

		#endregion

		#region operation: Page

		/// <summary>
		/// Starting with a regular SELECT statement, derives the SQL statements required to query a 
		/// DB for a page of records and the total number of records
		/// </summary>
		/// <typeparam name="T">The Type representing a row in the result set</typeparam>
		/// <param name="skip">The number of rows to skip before the start of the page</param>
		/// <param name="take">The number of rows in the page</param>
		/// <param name="sql">The original SQL select statement</param>
		/// <param name="args">Arguments to any embedded parameters in the SQL</param>
		/// <param name="sqlCount">Outputs the SQL statement to query for the total number of matching rows</param>
		/// <param name="sqlPage">Outputs the SQL statement to retrieve a single page of matching rows</param>
		void BuildPageQueries<T>(long skip, long take, string sql, ref object[] args, out string sqlCount, out string sqlPage)
		{
			// Add auto select clause
			if(EnableAutoSelect)
				sql = AutoSelectHelper.AddSelectClause<T>(_dbType, sql);

			// Split the SQL
			PagingHelper.SQLParts parts;
			if(!PagingHelper.SplitSQL(sql, out parts))
				throw new Exception("Unable to parse SQL statement for paged query");

			sqlPage = _dbType.BuildPageQuery(skip, take, parts, ref args);
			sqlCount = parts.sqlCount;
		}

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="page">从 1 开始的页码</param>
		/// <param name="itemsPerPage">每页记录数</param>
		/// <param name="sqlCount">用于获取总记录数的 SQL 语句</param>
		/// <param name="countArgs">用于获取总记录数的 SQL 语句的参数</param>
		/// <param name="sqlPage">用于获取分页结果的 SQL 语句</param>
		/// <param name="pageArgs">用于获取分页结果的 SQL 语句的参数</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// This method allows separate SQL statements to be explicitly provided for the two parts of the page query.
		/// The page and itemsPerPage parameters are not used directly and are used simply to populate the returned Page object.
		/// </remarks>
		public Page<T> Page<T>(long page, long itemsPerPage, string sqlCount, object[] countArgs, string sqlPage, object[] pageArgs)
		{
			// Save the one-time command time out and use it for both queries
			var saveTimeout = OneTimeCommandTimeout;

			// Setup the paged result
			var result = new Page<T>
			{
				CurrentPage = page,
				ItemsPerPage = itemsPerPage,
				TotalItems = ExecuteScalar<long>(sqlCount, countArgs)
			};
			result.TotalPages = result.TotalItems / itemsPerPage;

			if((result.TotalItems % itemsPerPage) != 0)
				result.TotalPages++;

			OneTimeCommandTimeout = saveTimeout;

			// Get the records
			result.Items = Fetch<T>(sqlPage, pageArgs);

			// Done
			return result;
		}

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="page">从 1 开始的页码</param>
		/// <param name="itemsPerPage">每页记录数</param>
		/// <param name="sql">基本 SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.  It will also execute a second query to retrieve the
		/// total number of records in the result set.
		/// </remarks>
		public Page<T> Page<T>(long page, long itemsPerPage, string sql, params object[] args)
		{
			string sqlCount, sqlPage;
			BuildPageQueries<T>((page - 1) * itemsPerPage, itemsPerPage, sql, ref args, out sqlCount, out sqlPage);
			return Page<T>(page, itemsPerPage, sqlCount, args, sqlPage, args);
		}

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="page">从 1 开始的页码</param>
		/// <param name="itemsPerPage">每页记录数</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.  It will also execute a second query to retrieve the
		/// total number of records in the result set.
		/// </remarks>
		public Page<T> Page<T>(long page, long itemsPerPage, Sql sql)
		{
			return Page<T>(page, itemsPerPage, sql.SQL, sql.Arguments);
		}

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="page">从 1 开始的页码</param>
		/// <param name="itemsPerPage">每页记录数</param>
		/// <param name="sqlCount">用于获取总记录数的 SQL 语句生成器对象</param>
		/// <param name="sqlPage">用于获取分页结果的 SQL 语句生成器对象</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// This method allows separate SQL statements to be explicitly provided for the two parts of the page query.
		/// The page and itemsPerPage parameters are not used directly and are used simply to populate the returned Page object.
		/// </remarks>
		public Page<T> Page<T>(long page, long itemsPerPage, Sql sqlCount, Sql sqlPage)
		{
			return Page<T>(page, itemsPerPage, sqlCount.SQL, sqlCount.Arguments, sqlPage.SQL, sqlPage.Arguments);
		}

		#endregion

		#region operation: Fetch (page)

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="page">从 1 开始的页码</param>
		/// <param name="itemsPerPage">每页记录数</param>
		/// <param name="sql">基本 SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.
		/// </remarks>
		public List<T> Fetch<T>(long page, long itemsPerPage, string sql, params object[] args)
		{
			return SkipTake<T>((page - 1) * itemsPerPage, itemsPerPage, sql, args);
		}

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="page">从 1 开始的页码</param>
		/// <param name="itemsPerPage">每页记录数</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified page.
		/// </remarks>
		public List<T> Fetch<T>(long page, long itemsPerPage, Sql sql)
		{
			return SkipTake<T>((page - 1) * itemsPerPage, itemsPerPage, sql.SQL, sql.Arguments);
		}

		#endregion

		#region operation: SkipTake

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="skip">要跳过的记录数</param>
		/// <param name="take">要获取的记录数</param>
		/// <param name="sql">基本 SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified range.
		/// </remarks>
		public List<T> SkipTake<T>(long skip, long take, string sql, params object[] args)
		{
			string sqlCount, sqlPage;
			BuildPageQueries<T>(skip, take, sql, ref args, out sqlCount, out sqlPage);
			return Fetch<T>(sqlPage, args);
		}

		/// <summary>获取分页查询结果</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="skip">要跳过的记录数</param>
		/// <param name="take">要获取的记录数</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>分页结果</returns>
		/// <remarks>
		/// PetaPoco will automatically modify the supplied SELECT statement to only retrieve the
		/// records for the specified range.
		/// </remarks>
		public List<T> SkipTake<T>(long skip, long take, Sql sql)
		{
			return SkipTake<T>(skip, take, sql.SQL, sql.Arguments);
		}
		#endregion

		#region operation: Query

		/// <summary>获取结果集</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List rather than an IEnumerable.
		/// </remarks>
		public IEnumerable<T> Query<T>(string sql, params object[] args)
		{
			if(EnableAutoSelect)
				sql = AutoSelectHelper.AddSelectClause<T>(_dbType, sql);

			OpenSharedConnection();
			try
			{
				using(var cmd = CreateCommand(_sharedConnection, sql, args))
				{
					IDataReader r;
					var pd = PocoData.ForType(typeof(T));
					try
					{
						r = cmd.ExecuteReader();
						OnExecutedCommand(cmd);
					}
					catch(Exception x)
					{
						if(OnException(x))
							throw;
						yield break;
					}
					var factory = pd.GetFactory(cmd.CommandText, _sharedConnection.ConnectionString, 0, r.FieldCount, r) as Func<IDataReader, T>;
					using(r)
					{
						while(true)
						{
							T poco;
							try
							{
								if(!r.Read())
									yield break;
								poco = factory(r);
							}
							catch(Exception x)
							{
								if(OnException(x))
									throw;
								yield break;
							}

							yield return poco;
						}
					}
				}
			}
			finally
			{
				CloseSharedConnection();
			}
		}

		/// <summary>获取结果集</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		/// <remarks>
		/// For some DB providers, care should be taken to not start a new Query before finishing with
		/// and disposing the previous one. In cases where this is an issue, consider using Fetch which
		/// returns the results as a List rather than an IEnumerable.
		/// </remarks>
		public IEnumerable<T> Query<T>(Sql sql)
		{
			return Query<T>(sql.SQL, sql.Arguments);
		}

		#endregion

		#region operation: Exists

		/// <summary>检测记录是否存在</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sqlCondition">SQL 条件语句</param>
		/// <param name="args">参数</param>
		/// <returns>返回指示记录是否存在</returns>
		public bool Exists<T>(string sqlCondition, params object[] args)
		{
			var poco = PocoData.ForType(typeof(T)).TableInfo;

			return ExecuteScalar<int>(string.Format(_dbType.GetExistsSql(), poco.TableName, sqlCondition), args) != 0;
		}

		/// <summary>检测记录是否存在</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="primaryKey">主键名</param>
		/// <returns>返回指示记录是否存在</returns>
		public bool Exists<T>(object primaryKey)
		{
			return Exists<T>(string.Format("{0}=@0", _dbType.EscapeSqlIdentifier(PocoData.ForType(typeof(T)).TableInfo.PrimaryKey)), primaryKey);
		}

		#endregion

		#region operation: linq style (Exists, Single, SingleOrDefault etc...)

		/// <summary>获取单个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="primaryKey">主键名</param>
		/// <returns>实体对象</returns>
		/// <remarks>
		/// Throws an exception if there are zero or more than one record with the specified primary key value.
		/// </remarks>
		public T Single<T>(object primaryKey)
		{
			return Single<T>(string.Format("WHERE {0}=@0", _dbType.EscapeSqlIdentifier(PocoData.ForType(typeof(T)).TableInfo.PrimaryKey)), primaryKey);
		}

		/// <summary>获取单个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="primaryKey">主键名</param>
		/// <returns>实体对象</returns>
		/// <remarks>
		/// If there are no records with the specified primary key value, default(T) (typically null) is returned.
		/// </remarks>
		public T SingleOrDefault<T>(object primaryKey)
		{
			return SingleOrDefault<T>(string.Format("WHERE {0}=@0", _dbType.EscapeSqlIdentifier(PocoData.ForType(typeof(T)).TableInfo.PrimaryKey)), primaryKey);
		}

		/// <summary>获取单个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>实体对象</returns>
		/// <remarks>
		/// Throws an exception if there are zero or more than one matching record
		/// </remarks>
		public T Single<T>(string sql, params object[] args)
		{
			return Query<T>(sql, args).Single();
		}

		/// <summary>获取单个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>实体对象</returns>
		public T SingleOrDefault<T>(string sql, params object[] args)
		{
			return Query<T>(sql, args).SingleOrDefault();
		}

		/// <summary>获取第一个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>实体对象</returns>
		public T First<T>(string sql, params object[] args)
		{
			return Query<T>(sql, args).First();
		}

		/// <summary>获取第一个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>实体对象</returns>
		public T FirstOrDefault<T>(string sql, params object[] args)
		{
			return Query<T>(sql, args).FirstOrDefault();
		}


		/// <summary>获取单个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>实体对象</returns>
		/// <remarks>
		/// Throws an exception if there are zero or more than one matching record
		/// </remarks>
		public T Single<T>(Sql sql)
		{
			return Query<T>(sql).Single();
		}

		/// <summary>获取单个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>实体对象</returns>
		public T SingleOrDefault<T>(Sql sql)
		{
			return Query<T>(sql).SingleOrDefault();
		}

		/// <summary>获取第一个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>实体对象</returns>
		public T First<T>(Sql sql)
		{
			return Query<T>(sql).First();
		}

		/// <summary>获取第一个记录</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>实体对象</returns>
		public T FirstOrDefault<T>(Sql sql)
		{
			return Query<T>(sql).FirstOrDefault();
		}
		#endregion

		#region operation: Insert

		/// <summary>
		/// 执行插入命令。
		/// <para>对于有自增主键的返回插入后的值，并更新实体相应的值</para>
		/// <para>对于有主键但不是自增的返回原值</para>
		/// <para>对于没有主键的返回 <c>null</c></para>
		/// </summary>
		/// <param name="tableName">数据表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要插入的实体</param>
		/// <returns>返回插入后的主键值</returns>
		public object Insert(string tableName, string primaryKeyName, object poco)
		{
			return Insert(tableName, primaryKeyName, true, poco);
		}



		/// <summary>
		/// 执行插入命令。
		/// <para>对于有自增主键的返回插入后的值，并更新实体相应的值</para>
		/// <para>对于有主键但不是自增的返回原值</para>
		/// <para>对于没有主键的返回 <c>null</c></para>
		/// </summary>
		/// <param name="tableName">数据表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="autoIncrement">主键是否为自增列</param>
		/// <param name="poco">要插入的实体</param>
		/// <returns>返回插入后的主键值</returns>
		/// <remarks>Inserts a poco into a table.  If the poco has a property with the same name 
		/// as the primary key the id of the new record is assigned to it.  Either way,
		/// the new id is returned.</remarks>
		public object Insert(string tableName, string primaryKeyName, bool autoIncrement, object poco)
		{
			try
			{
				OpenSharedConnection();
				try
				{
					using(var cmd = CreateCommand(_sharedConnection, ""))
					{
						var pd = PocoData.ForObject(poco, primaryKeyName);
						var names = new List<string>();
						var values = new List<string>();
						var index = 0;
						foreach(var i in pd.Columns)
						{
							// Don't insert result columns
							if(i.Value.ResultColumn)
								continue;

							// Don't insert the primary key (except under oracle where we need bring in the next sequence value)
							if(autoIncrement && primaryKeyName != null && string.Compare(i.Key, primaryKeyName, true) == 0)
							{
								// Setup auto increment expression
								string autoIncExpression = _dbType.GetAutoIncrementExpression(pd.TableInfo);
								if(autoIncExpression != null)
								{
									names.Add(i.Key);
									values.Add(autoIncExpression);
								}
								continue;
							}

							names.Add(_dbType.EscapeSqlIdentifier(i.Key));
							values.Add(string.Format("{0}{1}", _paramPrefix, index++));
							AddParam(cmd, i.Value.GetValue(poco), i.Value.PropertyInfo);
						}

						string outputClause = String.Empty;
						if(autoIncrement)
						{
							outputClause = _dbType.GetInsertOutputClause(primaryKeyName);
						}


						cmd.CommandText = string.Format("INSERT INTO {0} ({1}){2} VALUES ({3})",
								_dbType.EscapeTableName(tableName),
								string.Join(",", names.ToArray()),
								outputClause,
								string.Join(",", values.ToArray())
								);

						if(!autoIncrement)
						{
							DoPreExecute(cmd);
							cmd.ExecuteNonQuery();
							OnExecutedCommand(cmd);

							PocoColumn pkColumn;
							if(primaryKeyName != null && pd.Columns.TryGetValue(primaryKeyName, out pkColumn))
								return pkColumn.GetValue(poco);
							else
								return null;
						}


						object id = _dbType.ExecuteInsert(this, cmd, primaryKeyName);


						// Assign the ID back to the primary key property
						if(primaryKeyName != null)
						{
							PocoColumn pc;
							if(pd.Columns.TryGetValue(primaryKeyName, out pc))
							{
								pc.SetValue(poco, pc.ChangeType(id));
							}
						}

						return id;
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch(Exception x)
			{
				if(OnException(x))
					throw;
				return null;
			}
		}

		/// <summary>
		/// 执行插入命令。
		/// <para>对于有自增主键的返回插入后的值，并更新实体相应的值</para>
		/// <para>对于有主键但不是自增的返回原值</para>
		/// <para>对于没有主键的返回 <c>null</c></para>
		/// </summary>
		/// <param name="poco">要插入的实体</param>
		/// <returns>返回插入后的主键值</returns>
		/// <remarks>
		/// The name of the table, it's primary key and whether it's an auto-allocated primary key are retrieved
		/// from the POCO's attributes
		/// </remarks>
		public object Insert(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			return Insert(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, pd.TableInfo.AutoIncrement, poco);
		}

		#endregion

		#region operation: Update

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="tableName">数据表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要更新的实体</param>
		/// <param name="primaryKeyValue">主键值</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(string tableName, string primaryKeyName, object poco, object primaryKeyValue)
		{
			return Update(tableName, primaryKeyName, poco, primaryKeyValue, null);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="tableName">数据表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要更新的实体</param>
		/// <param name="primaryKeyValue">主键值</param>
		/// <param name="columns">要更新的字段名集合</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(string tableName, string primaryKeyName, object poco, object primaryKeyValue, IEnumerable<string> columns)
		{
			try
			{
				OpenSharedConnection();
				try
				{
					using(var cmd = CreateCommand(_sharedConnection, ""))
					{
						var sb = new StringBuilder();
						var index = 0;
						var pd = PocoData.ForObject(poco, primaryKeyName);
						if(columns == null)
						{
							foreach(var i in pd.Columns)
							{
								// Don't update the primary key, but grab the value if we don't have it
								if(string.Compare(i.Key, primaryKeyName, true) == 0)
								{
									if(primaryKeyValue == null)
										primaryKeyValue = i.Value.GetValue(poco);
									continue;
								}

								// Dont update result only columns
								if(i.Value.ResultColumn)
									continue;

								// Build the sql
								if(index > 0)
									sb.Append(", ");
								sb.AppendFormat("{0} = {1}{2}", _dbType.EscapeSqlIdentifier(i.Key), _paramPrefix, index++);

								// Store the parameter in the command
								AddParam(cmd, i.Value.GetValue(poco), i.Value.PropertyInfo);
							}
						}
						else
						{
							foreach(var colname in columns)
							{
								var pc = pd.Columns[colname];

								// Build the sql
								if(index > 0)
									sb.Append(", ");
								sb.AppendFormat("{0} = {1}{2}", _dbType.EscapeSqlIdentifier(colname), _paramPrefix, index++);

								// Store the parameter in the command
								AddParam(cmd, pc.GetValue(poco), pc.PropertyInfo);
							}

							// Grab primary key value
							if(primaryKeyValue == null)
							{
								var pc = pd.Columns[primaryKeyName];
								primaryKeyValue = pc.GetValue(poco);
							}

						}

						// Find the property info for the primary key
						PropertyInfo pkpi = null;
						if(primaryKeyName != null)
						{
							pkpi = pd.Columns[primaryKeyName].PropertyInfo;
						}

						cmd.CommandText = string.Format("UPDATE {0} SET {1} WHERE {2} = {3}{4}",
											_dbType.EscapeTableName(tableName), sb.ToString(), _dbType.EscapeSqlIdentifier(primaryKeyName), _paramPrefix, index++);
						AddParam(cmd, primaryKeyValue, pkpi);

						DoPreExecute(cmd);

						// Do it
						var retv = cmd.ExecuteNonQuery();
						OnExecutedCommand(cmd);
						return retv;
					}
				}
				finally
				{
					CloseSharedConnection();
				}
			}
			catch(Exception x)
			{
				if(OnException(x))
					throw;
				return -1;
			}
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="tableName">数据表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要更新的实体</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(string tableName, string primaryKeyName, object poco)
		{
			return Update(tableName, primaryKeyName, poco, null);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="tableName">数据表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要更新的实体</param>
		/// <param name="columns">要更新的字段名集合</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(string tableName, string primaryKeyName, object poco, IEnumerable<string> columns)
		{
			return Update(tableName, primaryKeyName, poco, null, columns);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="poco">要更新的实体</param>
		/// <param name="columns">要更新的字段名集合</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(object poco, IEnumerable<string> columns)
		{
			return Update(poco, null, columns);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="poco">要更新的实体</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(object poco)
		{
			return Update(poco, null, null);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="poco">要更新的实体</param>
		/// <param name="primaryKeyValue">主键值</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(object poco, object primaryKeyValue)
		{
			return Update(poco, primaryKeyValue, null);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="poco">要更新的实体</param>
		/// <param name="primaryKeyValue">主键值</param>
		/// <param name="columns">要更新的字段名集合</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update(object poco, object primaryKeyValue, IEnumerable<string> columns)
		{
			var pd = PocoData.ForType(poco.GetType());
			return Update(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, poco, primaryKeyValue, columns);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">更新和条件部分的 SQL 语句（“UPDATE 表名”后面的部分）</param>
		/// <param name="args">参数</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update<T>(string sql, params object[] args)
		{
			var pd = PocoData.ForType(typeof(T));
			return Execute(string.Format("UPDATE {0} {1}", _dbType.EscapeTableName(pd.TableInfo.TableName), sql), args);
		}

		/// <summary>执行更新命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Update<T>(Sql sql)
		{
			var pd = PocoData.ForType(typeof(T));
			return Execute(new Sql(string.Format("UPDATE {0}", _dbType.EscapeTableName(pd.TableInfo.TableName))).Append(sql));
		}
		#endregion

		#region operation: Delete

		/// <summary>执行删除命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="tableName">表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要更新的实体</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Delete(string tableName, string primaryKeyName, object poco)
		{
			return Delete(tableName, primaryKeyName, poco, null);
		}

		/// <summary>执行删除命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="tableName">表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要更新的实体</param>
		/// <param name="primaryKeyValue">主键值</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Delete(string tableName, string primaryKeyName, object poco, object primaryKeyValue)
		{
			// If primary key value not specified, pick it up from the object
			if(primaryKeyValue == null)
			{
				var pd = PocoData.ForObject(poco, primaryKeyName);
				PocoColumn pc;
				if(pd.Columns.TryGetValue(primaryKeyName, out pc))
				{
					primaryKeyValue = pc.GetValue(poco);
				}
			}

			// Do it
			var sql = string.Format("DELETE FROM {0} WHERE {1}=@0", _dbType.EscapeTableName(tableName), _dbType.EscapeSqlIdentifier(primaryKeyName));
			return Execute(sql, primaryKeyValue);
		}

		/// <summary>执行删除命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <param name="poco">要更新的实体</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Delete(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			return Delete(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, poco);
		}

		/// <summary>执行删除命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="pocoOrPrimaryKey">主键值</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Delete<T>(object pocoOrPrimaryKey)
		{
			if(pocoOrPrimaryKey.GetType() == typeof(T))
				return Delete(pocoOrPrimaryKey);
			var pd = PocoData.ForType(typeof(T));
			return Delete(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, null, pocoOrPrimaryKey);
		}

		/// <summary>执行删除命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">条件部分的 SQL 语句（“DELETE FROM 表名”后面的部分）</param>
		/// <param name="args">参数</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Delete<T>(string sql, params object[] args)
		{
			var pd = PocoData.ForType(typeof(T));
			return Execute(string.Format("DELETE FROM {0} {1}", _dbType.EscapeTableName(pd.TableInfo.TableName), sql), args);
		}

		/// <summary>执行删除命令，并返回受影响的行数，返回 -1 表示执行命令出错</summary>
		/// <typeparam name="T">实体类型</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>返回受影响的行数，返回 -1 表示执行命令出错</returns>
		public int Delete<T>(Sql sql)
		{
			var pd = PocoData.ForType(typeof(T));
			return Execute(new Sql(string.Format("DELETE FROM {0}", _dbType.EscapeTableName(pd.TableInfo.TableName))).Append(sql));
		}
		#endregion

		#region operation: IsNew

		/// <summary>检测实体是否为新记录</summary>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要检测的实体</param>
		/// <returns>是否为新记录</returns>
		/// <exception cref="System.ArgumentException">找不到主键</exception>
		/// <remarks>
		/// This method simply tests if the POCO's primary key column property has been set to something non-zero.
		/// </remarks>
		public bool IsNew(string primaryKeyName, object poco)
		{
			var pd = PocoData.ForObject(poco, primaryKeyName);
			object pk;
			PocoColumn pc;
			if(pd.Columns.TryGetValue(primaryKeyName, out pc))
			{
				pk = pc.GetValue(poco);
			}
#if !PETAPOCO_NO_DYNAMIC
			else if(poco.GetType() == typeof(System.Dynamic.ExpandoObject))
			{
				return true;
			}
#endif
			else
			{
				var pi = poco.GetType().GetProperty(primaryKeyName);
				if(pi == null)
					throw new ArgumentException(Localization.Resource.PKNameNotFoundException.F(poco.GetType(), primaryKeyName));
				pk = pi.FastGetValue(poco);
			}

			if(pk == null)
				return true;

			var type = pk.GetType();

			if(type.IsValueType)
			{
				// Common primary key types
				if(type == typeof(long))
					return (long)pk == default(long);
				else if(type == typeof(ulong))
					return (ulong)pk == default(ulong);
				else if(type == typeof(int))
					return (int)pk == default(int);
				else if(type == typeof(uint))
					return (uint)pk == default(uint);
				else if(type == typeof(Guid))
					return (Guid)pk == default(Guid);

				// Create a default instance and compare
				return pk == pk.GetType().FastInvoke();
			}
			else
			{
				return pk == null;
			}
		}

		/// <summary>检测实体是否为新记录</summary>
		/// <param name="poco">要检测的实体</param>
		/// <returns>是否为新记录</returns>
		/// <exception cref="System.InvalidOperationException">没有定义自增主键</exception>
		/// <remarks>
		/// This method simply tests if the POCO's primary key column property has been set to something non-zero.
		/// </remarks>
		public bool IsNew(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			if(!pd.TableInfo.AutoIncrement)
				throw new InvalidOperationException(Localization.Resource.AutoIncrementPKNotFoundException.F(pd.TableInfo.TableName));
			return IsNew(pd.TableInfo.PrimaryKey, poco);
		}
		#endregion

		#region operation: Save
		/// <summary>保存实体（自动判断插入或更新）</summary>
		/// <param name="tableName">表名</param>
		/// <param name="primaryKeyName">主键名</param>
		/// <param name="poco">要保存的实体</param>
		public void Save(string tableName, string primaryKeyName, object poco)
		{
			if(IsNew(primaryKeyName, poco))
			{
				Insert(tableName, primaryKeyName, true, poco);
			}
			else
			{
				Update(tableName, primaryKeyName, poco);
			}
		}

		/// <summary>保存实体（自动判断插入或更新）</summary>
		/// <param name="poco">要保存的实体</param>
		public void Save(object poco)
		{
			var pd = PocoData.ForType(poco.GetType());
			Save(pd.TableInfo.TableName, pd.TableInfo.PrimaryKey, poco);
		}
		#endregion

		#region operation: Multi-Poco Query/Fetch
		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public List<TRet> Fetch<T1, T2, TRet>(Func<T1, T2, TRet> cb, string sql, params object[] args) { return Query<T1, T2, TRet>(cb, sql, args).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public List<TRet> Fetch<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, string sql, params object[] args) { return Query<T1, T2, T3, TRet>(cb, sql, args).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public List<TRet> Fetch<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, string sql, params object[] args) { return Query<T1, T2, T3, T4, TRet>(cb, sql, args).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<T1, T2, TRet>(Func<T1, T2, TRet> cb, string sql, params object[] args) { return Query<TRet>(new Type[] { typeof(T1), typeof(T2) }, cb, sql, args); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, string sql, params object[] args) { return Query<TRet>(new Type[] { typeof(T1), typeof(T2), typeof(T3) }, cb, sql, args); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, string sql, params object[] args) { return Query<TRet>(new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, cb, sql, args); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public List<TRet> Fetch<T1, T2, TRet>(Func<T1, T2, TRet> cb, Sql sql) { return Query<T1, T2, TRet>(cb, sql.SQL, sql.Arguments).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public List<TRet> Fetch<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, Sql sql) { return Query<T1, T2, T3, TRet>(cb, sql.SQL, sql.Arguments).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public List<TRet> Fetch<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, Sql sql) { return Query<T1, T2, T3, T4, TRet>(cb, sql.SQL, sql.Arguments).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<T1, T2, TRet>(Func<T1, T2, TRet> cb, Sql sql) { return Query<TRet>(new Type[] { typeof(T1), typeof(T2) }, cb, sql.SQL, sql.Arguments); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<T1, T2, T3, TRet>(Func<T1, T2, T3, TRet> cb, Sql sql) { return Query<TRet>(new Type[] { typeof(T1), typeof(T2), typeof(T3) }, cb, sql.SQL, sql.Arguments); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <typeparam name="TRet">返回的结果实体类型</typeparam>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<T1, T2, T3, T4, TRet>(Func<T1, T2, T3, T4, TRet> cb, Sql sql) { return Query<TRet>(new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, cb, sql.SQL, sql.Arguments); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public List<T1> Fetch<T1, T2>(string sql, params object[] args) { return Query<T1, T2>(sql, args).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public List<T1> Fetch<T1, T2, T3>(string sql, params object[] args) { return Query<T1, T2, T3>(sql, args).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public List<T1> Fetch<T1, T2, T3, T4>(string sql, params object[] args) { return Query<T1, T2, T3, T4>(sql, args).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<T1> Query<T1, T2>(string sql, params object[] args) { return Query<T1>(new Type[] { typeof(T1), typeof(T2) }, null, sql, args); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<T1> Query<T1, T2, T3>(string sql, params object[] args) { return Query<T1>(new Type[] { typeof(T1), typeof(T2), typeof(T3) }, null, sql, args); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<T1> Query<T1, T2, T3, T4>(string sql, params object[] args) { return Query<T1>(new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, sql, args); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public List<T1> Fetch<T1, T2>(Sql sql) { return Query<T1, T2>(sql.SQL, sql.Arguments).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public List<T1> Fetch<T1, T2, T3>(Sql sql) { return Query<T1, T2, T3>(sql.SQL, sql.Arguments).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public List<T1> Fetch<T1, T2, T3, T4>(Sql sql) { return Query<T1, T2, T3, T4>(sql.SQL, sql.Arguments).ToList(); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public IEnumerable<T1> Query<T1, T2>(Sql sql) { return Query<T1>(new Type[] { typeof(T1), typeof(T2) }, null, sql.SQL, sql.Arguments); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public IEnumerable<T1> Query<T1, T2, T3>(Sql sql) { return Query<T1>(new Type[] { typeof(T1), typeof(T2), typeof(T3) }, null, sql.SQL, sql.Arguments); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="T1">实体类型1</typeparam>
		/// <typeparam name="T2">实体类型2</typeparam>
		/// <typeparam name="T3">实体类型3</typeparam>
		/// <typeparam name="T4">实体类型4</typeparam>
		/// <param name="sql">SQL 语句生成器对象</param>
		/// <returns>结果集</returns>
		public IEnumerable<T1> Query<T1, T2, T3, T4>(Sql sql) { return Query<T1>(new Type[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) }, null, sql.SQL, sql.Arguments); }

		/// <summary>获取结果集</summary>
		/// <typeparam name="TRet">结果实体类型</typeparam>
		/// <param name="types">多个实体类型</param>
		/// <param name="cb">实体间的关联转换器</param>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>结果集</returns>
		public IEnumerable<TRet> Query<TRet>(Type[] types, object cb, string sql, params object[] args)
		{
			OpenSharedConnection();
			try
			{
				using(var cmd = CreateCommand(_sharedConnection, sql, args))
				{
					IDataReader r;
					try
					{
						r = cmd.ExecuteReader();
						OnExecutedCommand(cmd);
					}
					catch(Exception x)
					{
						if(OnException(x))
							throw;
						yield break;
					}
					var factory = MultiPocoFactory.GetFactory<TRet>(types, _sharedConnection.ConnectionString, sql, r);
					if(cb == null)
						cb = MultiPocoFactory.GetAutoMapper(types.ToArray());
					bool bNeedTerminator = false;
					using(r)
					{
						while(true)
						{
							TRet poco;
							try
							{
								if(!r.Read())
									break;
								poco = factory(r, cb);
							}
							catch(Exception x)
							{
								if(OnException(x))
									throw;
								yield break;
							}

							if(poco != null)
								yield return poco;
							else
								bNeedTerminator = true;
						}
						if(bNeedTerminator)
						{
							var poco = (TRet)(cb as Delegate).DynamicInvoke(new object[types.Length]);
							if(poco != null)
								yield return poco;
							else
								yield break;
						}
					}
				}
			}
			finally
			{
				CloseSharedConnection();
			}
		}

		#endregion

		#region Last Command

		/// <summary>获取最后执行的 SQL 语句</summary>
		/// <value>最后执行的 SQL 语句</value>
		public string LastSQL { get { return _lastSql; } }

		/// <summary>获取最后执行 SQL 语句所用的参数</summary>
		/// <value>最后使用的参数</value>
		public object[] LastArgs { get { return _lastArgs; } }


		/// <summary>获取最后执行的命令（包含 SQL 语句和参数）</summary>
		/// <value>最后执行的命令</value>
		public string LastCommand
		{
			get { return FormatCommand(_lastSql, _lastArgs); }
		}
		#endregion

		#region FormatCommand

		/// <summary>以友好方式格式化命令信息（SQL 语句和参数）</summary>
		/// <param name="cmd">命令</param>
		/// <returns>格式化后的 SQL 语句和参数</returns>
		public string FormatCommand(IDbCommand cmd)
		{
			return FormatCommand(cmd.CommandText, (from IDataParameter parameter in cmd.Parameters select parameter.Value).ToArray());
		}

		/// <summary>以友好方式格式化 SQL 语句和参数</summary>
		/// <param name="sql">SQL 语句</param>
		/// <param name="args">参数</param>
		/// <returns>格式化后的 SQL 语句和参数</returns>
		public string FormatCommand(string sql, object[] args)
		{
			var sb = new StringBuilder();
			if(sql == null)
				return "";
			sb.Append(sql);
			if(args != null && args.Length > 0)
			{
				sb.Append("\n");
				for(int i = 0; i < args.Length; i++)
				{
					sb.AppendFormat("\t -> {0}{1} [{2}] = \"{3}\"\n", _paramPrefix, i, args[i].GetType().Name, args[i]);
				}
				sb.Remove(sb.Length - 1, 1);
			}
			return sb.ToString();
		}
		#endregion

		#region Public Properties

		/*
		public static IMapper Mapper
		{
			get;
			set;
		} */

		/// <summary>获取或设置是否在需要时自动创建“SELECT 字段”部分</summary>
		/// <value>指示是否在需要时自动创建“SELECT 字段”部分</value>
		public bool EnableAutoSelect
		{
			get;
			set;
		}

		/// <summary>获取或设置是否启用“@参数名”格式的参数名</summary>
		/// <value>指示是否启用“@参数名”格式的参数名</value>
		public bool EnableNamedParams
		{
			get;
			set;
		}

		/// <summary>获取或设置所有 SQL 语句每次执行的超时时间</summary>
		/// <value>所有 SQL 语句每次执行的超时时间</value>
		public int CommandTimeout
		{
			get;
			set;
		}

		/// <summary>获取或设置下一个 SQL 语句执行的超时时间</summary>
		/// <value>下一个 SQL 语句执行的超时时间</value>
		public int OneTimeCommandTimeout
		{
			get;
			set;
		}
		#endregion

		#region Member Fields
		// Member variables
		internal DatabaseType _dbType;
		string _connectionString;
		string _providerName;
		DbProviderFactory _factory;
		IDbConnection _sharedConnection;
		IDbTransaction _transaction;
		int _sharedConnectionDepth;
		int _transactionDepth;
		bool _transactionCancelled;
		string _lastSql;
		object[] _lastArgs;
		string _paramPrefix;
		#endregion

		#region Internal operations
		internal void ExecuteNonQueryHelper(IDbCommand cmd)
		{
			DoPreExecute(cmd);
			cmd.ExecuteNonQuery();
			OnExecutedCommand(cmd);
		}

		internal object ExecuteScalarHelper(IDbCommand cmd)
		{
			DoPreExecute(cmd);
			object r = cmd.ExecuteScalar();
			OnExecutedCommand(cmd);
			return r;
		}

		internal void DoPreExecute(IDbCommand cmd)
		{
			// Setup command timeout
			if(OneTimeCommandTimeout != 0)
			{
				cmd.CommandTimeout = OneTimeCommandTimeout;
				OneTimeCommandTimeout = 0;
			}
			else if(CommandTimeout != 0)
			{
				cmd.CommandTimeout = CommandTimeout;
			}

			// Call hook
			OnExecutingCommand(cmd);

			// Save it
			_lastSql = cmd.CommandText;
			_lastArgs = (from IDataParameter parameter in cmd.Parameters select parameter.Value).ToArray();
		}

		#endregion
	}
}