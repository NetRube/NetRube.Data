// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: EnumMapper.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

using System;
using System.Collections.Generic;

/// <summary>
/// Internal 命名空间
/// </summary>
namespace NetRube.Data.Internal
{
	internal static class EnumMapper
	{
		public static object EnumFromString(Type enumType, string value)
		{
			Dictionary<string, object> map = _types.Get(enumType, () =>
			{
				var values = Enum.GetValues(enumType);

				var newmap = new Dictionary<string, object>(values.Length, StringComparer.InvariantCultureIgnoreCase);

				foreach(var v in values)
				{
					newmap.Add(v.ToString(), v);
				}

				return newmap;
			});


			return map[value];
		}

		static Cache<Type, Dictionary<string, object>> _types = new Cache<Type, Dictionary<string, object>>();
	}
}
