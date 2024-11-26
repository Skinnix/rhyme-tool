using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScraperBase;

public static class IpaHelper
{
	private static readonly char[] STRESS_MARKERS = "ˈˌ".ToCharArray();
	private const string VOWELS = "iyɨʉɯuɪʏʊeøɘɵɤoəɛœɜɞʌɔæɐaɶɑɒ";

	public static bool IsVowel(char c) => VOWELS.Contains(c);
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
}
