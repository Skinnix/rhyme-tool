using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Dictionaries;

public static class StringExtensions
{
	public static string Reverse(this string s)
	{
		var arr = s.ToCharArray();
		Array.Reverse(arr);
		return new string(arr);
	}

	public static IEnumerable<char> ReverseEnumerator(this string s)
	{
		for (var i = s.Length - 1; i >= 0; i--)
			yield return s[i];
	}

	public static string ToLowerFirst(this string s)
	{
		if (s.Length == 0)
			return s;
		else if (s.Length == 1)
			return char.ToLowerInvariant(s[0]).ToString();

		return char.ToLowerInvariant(s[0]) + s[1..];
	}
}
