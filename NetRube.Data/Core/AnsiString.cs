// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: AnsiString.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

// PetaPoco - A Tiny ORMish thing for your POCO's.
// Copyright © 2011-2012 Topten Software.  All Rights Reserved.

/// <summary>
/// Data 命名空间
/// </summary>
namespace NetRube.Data
{
	/// <summary>
	/// 包装对应于 DBType.AnsiString 的 ANSI 字符串
	/// </summary>
	public class AnsiString
	{
		/// <summary>
		/// 初始化一个新 <see cref="AnsiString" /> 实例。
		/// </summary>
		/// <param name="str">要包装成 ANSI 字符串的 C# 字符串</param>
		public AnsiString(string str)
		{
			Value = str;
		}

		/// <summary>
		/// 字符串值
		/// </summary>
		/// <value>字符串值</value>
		public string Value
		{
			get;
			private set;
		}
	}
}