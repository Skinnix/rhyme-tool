﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool;

public static class StringExtensions
{
	public static IEnumerable<string> SplitAlternating(this string s, Func<char, bool> predicate)
	{
		if (s.Length == 0)
			yield break;

		//Prüfe den Start
		var condition = predicate(s[0]);
		var lastOffset = 0;
		for (var offset = 1; offset < s.Length; offset++)
		{
			//Lese bis zum nächsten Wechsel
			if (predicate(s[offset]) != condition)
			{
				//Gib den nächsten Abschnitt zurück
				yield return s[lastOffset..offset];
				condition = !condition;
				lastOffset = offset;
			}
		}

		//Gib den letzten Abschnitt zurück
		if (lastOffset < s.Length)
			yield return s[lastOffset..];
	}

	public static IEnumerable<string> SplitWhere(this string s, Func<char, char, bool> breakAt)
	{
		if (s.Length == 0)
			yield break;

		var lastOffset = 0;
		for (var offset = 1; offset < s.Length; offset++)
		{
			//Prüfe das aktuelle und das vorherige Zeichen
			var shouldBreak = breakAt(s[offset - 1], s[offset]);
			if (shouldBreak)
			{
				//Gib den nächsten Abschnitt zurück
				yield return s[lastOffset..offset];
				lastOffset = offset;
			}
		}

		//Gib den letzten Abschnitt zurück
		if (lastOffset < s.Length)
			yield return s[lastOffset..];
	}

	public static string ToUpperFirst(this string s)
	{
		if (s.Length == 0)
			return s;
		else if (s.Length == 1)
			return char.ToUpperInvariant(s[0]).ToString();

		return char.ToUpperInvariant(s[0]) + s[1..];
	}
}
