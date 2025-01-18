using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Rhyming;

public static class IpaHelper
{
	private static readonly char[] STRESS_MARKERS = "ˈˌ".ToCharArray();
	private const string VOWELS = "iyɨʉɯuɪʏʊeøɘɵɤoəɛœɜɞʌɔæɐaɶɑɒ";
	private const char SYLLABLE_MARKER = '̩';

	public static bool IsVowel(char c)
	{
		var normalized = c.ToString().Normalize(NormalizationForm.FormD);
		if (normalized.Length > 1)
			c = normalized[0];

		return VOWELS.Contains(c);
	}
	public static bool IsStressMarker(char c) => STRESS_MARKERS.Contains(c);

	public static string GetRhymeSyllable(string ipa)
	{
		var stressIndex = ipa.LastIndexOfAny(STRESS_MARKERS);
		if (stressIndex >= 0)
			ipa = ipa[(stressIndex + 1)..];

		var lastVowelIndex = ipa.IndexOfAny(VOWELS.ToCharArray());
		if (lastVowelIndex >= 0)
			ipa = ipa[lastVowelIndex..];

		return ipa;
	}

	public static IEnumerable<string> SplitSyllables(string ipa)
	{
		var vowelIndex = -1;
		var lastIndex = 0;
		for (var i = 0; i < ipa.Length; i++)
		{
			var c = ipa[i];
			if (IsStressMarker(c))
			{
				if (i == 0)
					continue;

				//Beginne neue Silbe
				yield return ipa[lastIndex..i];
				lastIndex = i;
				vowelIndex = -1;
				continue;
			}

			if (IsVowel(c))
			{
				if (vowelIndex == -1)
				{
					vowelIndex = i;
					continue;
				}

				if (vowelIndex == i - 1)
				{
					//Doppelvokal
					continue;
				}

				//Beginne neue Silbe
				yield return ipa[lastIndex..i];
				lastIndex = i;
				vowelIndex = i;
				continue;
			}

			if (c == SYLLABLE_MARKER)
			{
				//Beginne neue Silbe eins vorher
				yield return ipa[lastIndex..(i - 1)];
				lastIndex = i - 1;
				vowelIndex = -1;
				continue;
			}
		}

		if (lastIndex < ipa.Length)
			yield return ipa[lastIndex..];
	}

	public static string GetRhymeSuffix(string ipa, int maxSyllables = 3)
		=> string.Join(null, GetRhymeSuffixList(ipa, maxSyllables));

	public static string[] GetRhymeSuffixArray(string ipa, int maxSyllables = 3)
		=> GetRhymeSuffixList(ipa, maxSyllables).ToArray();

	private static List<string> GetRhymeSuffixList(string ipa, int maxSyllables = 3)
	{
		//Trenne Silben
		var syllables = SplitSyllables(ipa);

		//Verwende maximal die letzten {maxSyllables} Silben
		var lastSyllables = syllables.TakeLast(maxSyllables).ToList();

		//Ist eine davon betont?
		var stressedSyllableIndex = lastSyllables.FindLastIndex(s => s.Any(IsStressMarker));
		if (stressedSyllableIndex != -1)
			lastSyllables.RemoveRange(0, stressedSyllableIndex);

		//Setze die Silben wieder zusammen
		var vowelIndex = lastSyllables[0].Select((c, i) => (Char: c, Index: i))
			.First(c => IsVowel(c.Char))
			.Index;
		lastSyllables[0] = lastSyllables[0][vowelIndex..];
		return lastSyllables;
	}
}

[Obsolete]
public static class RhymeHelper1
{
	private const string VOWELS = "aeiouyäöü";
	private const string VOWEL_STRESSERS = VOWELS + "h";

	private static readonly char[] vowelsArray = VOWELS.ToCharArray();

	public static bool IsVowel(char c) => VOWELS.Contains(c);

	public static string GetRhymeSyllable(IEnumerable<string> syllables, out int syllableCount)
	{
		//Maximal 3 Silben
		var candidates = syllables.TakeLast(3).ToList();

		//Suche langen/betonten Vokal
		for (var i = candidates.Count - 1; i > 0; i--)
		{
			var syllable = candidates[i];
			var stressIndex = FindStressedVowelIndex(syllable);
			if (stressIndex >= 0)
			{
				//Entferne alles vor dem letzten betonten Vokal
				syllable = syllable[stressIndex..];
				candidates[i] = syllable;
				candidates.RemoveRange(0, i);
				break;
			}
		}

		//Füge das Wort wieder zusammen
		syllableCount = candidates.Count;
		var word = string.Join(null, candidates);

		//Finde den ersten Vokal
		var firstVowel = word.IndexOfAny(vowelsArray);
		if (firstVowel >= 0)
			word = word[firstVowel..];

		return word;
	}

	public static string GetRhymableSuffix(string suffix)
	{
		//Finde den ersten Vokal
		var firstVowel = suffix.IndexOfAny(vowelsArray);
		if (firstVowel >= 0)
			suffix = suffix[firstVowel..];

		return suffix;
	}

	private static int FindStressedVowelIndex(string syllable)
	{
		for (var i = 0; i < syllable.Length - 1; i++)
		{
			var letter = syllable[i];
			if (IsVowel(letter))
			{
				var next = syllable[i + 1];
				if (VOWEL_STRESSERS.Contains(next))
					return i;
				i++;
			}
		}

		return -1;
	}
}
