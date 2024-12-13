using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScraperBase;

public static class EnumerableExtensions
{
	public static T[]? OrNullIfEmpty<T>(this T[] array)
		=> array.Length == 0 ? null : array;
}
