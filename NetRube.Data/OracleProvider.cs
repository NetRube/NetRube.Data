using System;
using System.Data.Common;

namespace NetRube.Data
{
	/* 
	Thanks to Adam Schroder (@schotime) for this.
	
	This extra file provides an implementation of DbProviderFactory for early versions of the Oracle
	drivers that don't include include it.  For later versions of Oracle, the standard OracleProviderFactory
	class should work fine
	
	Uses reflection to load Oracle.DataAccess assembly and in-turn create connections and commands
	
	Currently untested.
	
	Usage:   
		
			new PetaPoco.Database("<connstring>", new PetaPoco.OracleProvider())
	
	Or in your app/web config (be sure to change ASSEMBLYNAME to the name of your 
	assembly containing OracleProvider.cs)
	
		<connectionStrings>
			<add
				name="oracle"
				connectionString="WHATEVER"
				providerName="Oracle"
				/>
		</connectionStrings>

		<system.data>
			<DbProviderFactories>
				<add name="PetaPoco Oracle Provider" invariant="Oracle" description="PetaPoco Oracle Provider" 
								type="PetaPoco.OracleProvider, ASSEMBLYNAME" />
			</DbProviderFactories>
		</system.data>

	 */

	/// <summary>Oracle 数据源适配器</summary>
	public class OracleProvider : DbProviderFactory
	{
		//private const string _assemblyName = "Oracle.DataAccess";
		//private const string _connectionTypeName = "Oracle.DataAccess.Client.OracleConnection";
		//private const string _commandTypeName = "Oracle.DataAccess.Client.OracleCommand";
		private const string _connectionTypeName = "Oracle.DataAccess.Client.OracleConnection, Oracle.DataAccess";
		private const string _commandTypeName = "Oracle.DataAccess.Client.OracleCommand, Oracle.DataAccess";
		private static Type _connectionType;
		private static Type _commandType;

		// Required for DbProviderFactories.GetFactory() to work.
		/// <summary>Oracle 数据源适配器实例</summary>
		public static OracleProvider Instance = new OracleProvider();

		/// <summary>初始化一个新 <see cref="OracleProvider"/> 实例。</summary>
		/// <exception cref="System.InvalidOperationException">Can't find Connection type:  + _connectionTypeName</exception>
		public OracleProvider()
		{
			//_connectionType = TypeFromAssembly(_connectionTypeName, _assemblyName);
			//_commandType = TypeFromAssembly(_commandTypeName, _assemblyName);
			_connectionType = FastReflection.FastGetType(_connectionTypeName);
			_commandType = FastReflection.FastGetType(_commandTypeName);
			if(_connectionType == null)
				throw new InvalidOperationException("Can't find Connection type: " + _connectionTypeName);
		}

		/// <summary>
		/// 返回实现 <see cref="T:System.Data.Common.DbConnection" /> 类的提供程序的类的一个新实例。
		/// </summary>
		/// <returns><see cref="T:System.Data.Common.DbConnection" /> 的新实例。</returns>
		public override DbConnection CreateConnection()
		{
			return _connectionType.FastInvoke<DbConnection>();
		}

		/// <summary>
		/// 返回实现 <see cref="T:System.Data.Common.DbCommand" /> 类的提供程序的类的一个新实例。
		/// </summary>
		/// <returns><see cref="T:System.Data.Common.DbCommand" /> 的新实例。</returns>
		public override DbCommand CreateCommand()
		{
			DbCommand command = _commandType.FastInvoke<DbCommand>();
			_commandType.GetProperty("BindByName").FastSetValue(command, true);

			return command;
		}

		//public static Type TypeFromAssembly(string typeName, string assemblyName)
		//{
		//	try
		//	{
		//		// Try to get the type from an already loaded assembly
		//		Type type = Type.GetType(typeName);

		//		if (type != null)
		//		{
		//			return type;
		//		}

		//		if (assemblyName == null)
		//		{
		//			// No assembly was specified for the type, so just fail
		//			string message = "Could not load type " + typeName + ". Possible cause: no assembly name specified.";
		//			throw new TypeLoadException(message);
		//		}

		//		Assembly assembly = Assembly.Load(assemblyName);

		//		if (assembly == null)
		//		{
		//			throw new InvalidOperationException("Can't find assembly: " + assemblyName);
		//		}

		//		type = assembly.GetType(typeName);

		//		if (type == null)
		//		{
		//			return null;
		//		}

		//		return type;
		//	}
		//	catch (Exception)
		//	{
		//		return null;
		//	}
		//}
	}
}
