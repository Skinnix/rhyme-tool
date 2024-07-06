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

	public static IEnumerable<T> StartBefore<T>(this IEnumerable<T> enumerable, T item) => StartBefore(enumerable, i => Equals(item, i));
	public static IEnumerable<T> StartBefore<T>(this IEnumerable<T> enumerable, Predicate<T> condition)
	{
		//Folge Leer?
		var enumerator = enumerable.GetEnumerator();
		if (!enumerator.MoveNext())
			yield break;

		//Erstes Element
		var previous = enumerator.Current;

		//Nur ein Element?
		if (!enumerator.MoveNext())
			yield break;

		//Prüfe Elemente, bis die Bedingung erfüllt ist
		while (!condition(enumerator.Current))
		{
			//Ist die Folge beendet?
			if (!enumerator.MoveNext())
			{
				//Element nicht gefunden
				yield break;
			}
		}

		//Gib das vorherige Element zurück, danach den Rest der Folge
		yield return previous;

		do
		{
			yield return enumerator.Current;
		}
		while (enumerator.MoveNext());
	}

	public static int InsertAfter<T>(this IList<T> list, T item, T insert)
	{
		var index = list.IndexOf(item);
		if (index == -1)
			throw new ArgumentException("Item not found", nameof(item));

		if (index == list.Count - 1)
		{
			list.Add(insert);
			return list.Count - 1;
		}
		else
		{
			list.Insert(index + 1, insert);
			return index + 1;
		}
	}

	public static int InsertAfter<T>(this List<T> list, T item, IEnumerable<T> insert)
	{
		var index = list.IndexOf(item);
		if (index == -1)
			throw new ArgumentException("Item not found", nameof(item));

		if (index == list.Count - 1)
		{
			list.AddRange(insert);
			return list.Count - 1;
		}
		else
		{
			list.InsertRange(index + 1, insert);
			return index + 1;
		}
	}

	public static int InsertBefore<T>(this IList<T> list, T item, T insert)
	{
		var index = list.IndexOf(item);
		if (index == -1)
			throw new ArgumentException("Item not found", nameof(item));

		list.Insert(index, insert);
		return index;
	}

	public static int InsertBefore<T>(this List<T> list, T item, IEnumerable<T> insert)
	{
		var index = list.IndexOf(item);
		if (index == -1)
			throw new ArgumentException("Item not found", nameof(item));

		list.InsertRange(index, insert);
		return index;
	}
}
