﻿// ***********************************************************************
// 程序集			: NetRube.Data
// 文件名			: ArrayKey.cs
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
	internal class ArrayKey<T>
	{
		public ArrayKey(T[] keys)
		{
			// Store the keys
			_keys = keys;

			// Calculate the hashcode
			_hashCode = 17;
			foreach(var k in keys)
			{
				_hashCode = _hashCode * 23 + (k == null ? 0 : k.GetHashCode());
			}
		}

		T[] _keys;
		int _hashCode;

		bool Equals(ArrayKey<T> other)
		{
			if(other == null)
				return false;

			if(other._hashCode != _hashCode)
				return false;

			if(other._keys.Length != _keys.Length)
				return false;

			for(int i = 0; i < _keys.Length; i++)
			{
				if(!object.Equals(_keys[i], other._keys[i]))
					return false;
			}

			return true;
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ArrayKey<T>);
		}

		public override int GetHashCode()
		{
			return _hashCode;
		}
	}
}