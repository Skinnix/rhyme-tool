using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Editing;

public static class ReferenceCache
{
	public static T Cache<T>(T reference)
		where T : class
		=> TypedCache<T>.Cache(reference);

	public static Either<T1, T2> Cache<T1, T2>(Either<T1, T2> reference)
		where T1 : class where T2 : class
		=> reference.Switch<Either<T1, T2>>(
			t1 => Cache(t1),
			t2 => Cache(t2));

	public static Either<T1, T2, T3> Cache<T1, T2, T3>(Either<T1, T2, T3> reference)
		where T1 : class where T2 : class where T3 : class
		=> reference.Switch<Either<T1, T2, T3>>(
			t1 => Cache(t1),
			t2 => Cache(t2),
			t3 => Cache(t3));

	public static Either<T1, T2, T3, T4> Cache<T1, T2, T3, T4>(Either<T1, T2, T3, T4> reference)
		where T1 : class where T2 : class where T3 : class where T4 : class
		=> reference.Switch<Either<T1, T2, T3, T4>>(
			t1 => Cache(t1),
			t2 => Cache(t2),
			t3 => Cache(t3),
			t4 => Cache(t4));

	public static Either<T1, T2, T3, T4, T5> Cache<T1, T2, T3, T4, T5>(Either<T1, T2, T3, T4, T5> reference)
		where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class
		=> reference.Switch<Either<T1, T2, T3, T4, T5>>(
			t1 => Cache(t1),
			t2 => Cache(t2),
			t3 => Cache(t3),
			t4 => Cache(t4),
			t5 => Cache(t5));

	private static class TypedCache<T>
		where T : class
	{
		private static readonly WeakCollection cache = new();

		public static T Cache(T reference)
		{
			lock (cache)
			{
				return cache.GetOrAdd(reference);
			}
		}

		private class WeakCollection : IReadOnlyCollection<T>
		{
			private readonly List<WeakReference<T>> items = new();

			public bool IsReadOnly => false;
			public int Count => items.Count;

			public bool Any() => items.Any(i => i.TryGetTarget(out _));

			public bool Contains(T item)
			{
				foreach (var i in this)
					if (i.Equals(item))
						return true;

				return false;
			}

			public void CopyTo(T[] array, int arrayIndex)
			{
				foreach (var i in this)
					array[arrayIndex++] = i;
			}

			public T GetOrAdd(T item)
			{
				foreach (var i in this)
					if (i.Equals(item))
						return i;

				items.Add(new(item));
				return item;
			}

			public bool Remove(T item)
			{
				for (var i = 0; i < items.Count; i++)
				{
					if (items[i].TryGetTarget(out var target) && target.Equals(item))
					{
						items.RemoveAt(i);
						return true;
					}
				}

				return false;
			}

			public void Clear() => items.Clear();

			public IEnumerator<T> GetEnumerator()
			{
				items.RemoveAll(i => !i.TryGetTarget(out _));

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
				items.RemoveAll(i => !i.TryGetTarget(out _));

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
