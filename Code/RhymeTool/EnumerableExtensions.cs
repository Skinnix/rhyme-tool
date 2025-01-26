using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool;

public static class EnumerableExtensions
{
	public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? enumerable)
		=> enumerable ?? Enumerable.Empty<T>();

	public static void Replace<T>(this IList<T> list, T oldItem, T newItem)
	{
		var index = list.IndexOf(oldItem);
		if (index == -1)
			throw new ArgumentException("Item not found", nameof(oldItem));

		list[index] = newItem;
	}

	public static T[]? OrNullIfEmpty<T>(this T[] array)
		=> array.Length == 0 ? null : array;

	public static int BinarySearch<T, TKey>(this IReadOnlyList<T> list, Func<T, TKey> keySelector, TKey key)
		where TKey : IComparable<TKey>
	{
		if (list.Count == 0)
			return -1;

		var min = 0;
		var max = list.Count;
		while (min < max)
		{
			var mid = min + ((max - min) >> 1);
			var midKey = keySelector(list[mid]);
			var comp = midKey.CompareTo(key);
			if (comp < 0)
				min = mid + 1;
			else if (comp > 0)
				max = mid - 1;
			else
				return mid;
		}

		if (min == max &&
			min < list.Count &&
			keySelector(list[min]).CompareTo(key) == 0)
			return min;

		return -1;
	}

	public static int BinarySearchWeak<TSource, T>(this TSource list, Func<T, int> comparer)
		where TSource : IReadOnlyList<T>
	{
		if (list.Count == 0)
			return -1;

		var min = 0;
		var max = list.Count;
		while (min < max)
		{
			var mid = min + ((max - min) >> 1);
			var midEntry = list[mid];
			var comp = comparer(midEntry);
			if (comp < 0)
				min = mid + 1;
			else if (comp > 0)
				max = mid - 1;
			else
				return mid;
		}

		if (min == max &&
			min < list.Count)
			return min;

		return -1;
	}
}
