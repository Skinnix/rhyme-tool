using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Editing;

public static class ArrayCache
{
	public static T[] Cache<T>(T[] array)
		=> TypedCache<T>.Cache(array);

	private static class TypedCache<T>
	{
		private static readonly SortedList<int, WeakCollection> cache = new();

		public static T[] Cache(T[] array)
		{
			lock (cache)
			{
				var length = array.Length;
				if (!cache.TryGetValue(length, out var collection))
					cache.Add(length, collection = new());

				return collection.GetOrAdd(array);
			}
		}

		private class WeakCollection : IReadOnlyCollection<T[]>
		{
			private readonly List<WeakReference<T[]>> items = new();

			public bool IsReadOnly => false;
			public int Count => items.Count;

			public bool Any() => items.Any(i => i.TryGetTarget(out _));

			public bool Contains(T[] item)
			{
				foreach (var i in this)
					if (i.SequenceEqual(item))
						return true;

				return false;
			}

			public void CopyTo(T[][] array, int arrayIndex)
			{
				foreach (var i in this)
					array[arrayIndex++] = i;
			}

			public T[] GetOrAdd(T[] item)
			{
				foreach (var i in this)
					if (i.SequenceEqual(item))
						return i;

				items.Add(new(item));
				return item;
			}

			public bool Remove(T[] item)
			{
				for (var i = 0; i < items.Count; i++)
				{
					if (items[i].TryGetTarget(out var target) && target.SequenceEqual(item))
					{
						items.RemoveAt(i);
						return true;
					}
				}

				return false;
			}

			public void Clear() => items.Clear();

			public IEnumerator<T[]> GetEnumerator()
			{
				for (var i = 0; i < items.Count; i++)
				{
					if (items[i].TryGetTarget(out var target))
						yield return target;
					else
						items.RemoveAt(i--);
				}
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
