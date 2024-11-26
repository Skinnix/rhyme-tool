using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScraperBase;

public static class BinaryWriterExtensions
{
	public static void WriteCollection<T>(this BinaryWriter writer, IReadOnlyCollection<T> collection, Action<T> itemAction)
	{
		writer.Write7BitEncodedInt(collection.Count);
		foreach (var item in collection)
			itemAction(item);
	}

	public static List<T> ReadCollection<T>(this BinaryReader reader, Func<T> readItem)
	{
		var count = reader.Read7BitEncodedInt();
		var result = new List<T>();
		for (var i = 0; i < count; i++)
			result.Add(readItem());

		return result;
	}
}
