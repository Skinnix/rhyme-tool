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
}
