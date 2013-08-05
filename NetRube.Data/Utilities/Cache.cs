
namespace NetRube.Data.Internal
{
	internal class Cache<TKey, TValue> : Dict<TKey, TValue> {
		public void Flush()
		{
			base.Clear();
		}
	}
}