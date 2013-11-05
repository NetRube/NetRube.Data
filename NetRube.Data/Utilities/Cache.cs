// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: Cache.cs
// 作者				: NetRube
// 创建时间			: 2013-08-05
//
// 最后修改者		: NetRube
// 最后修改时间		: 2013-11-05
// ***********************************************************************

/// <summary>
/// Internal 命名空间
/// </summary>
namespace NetRube.Data.Internal
{
	internal class Cache<TKey, TValue> : Dict<TKey, TValue> {
		public void Flush()
		{
			base.Clear();
		}
	}
}