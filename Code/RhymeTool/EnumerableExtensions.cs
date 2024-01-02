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
}
