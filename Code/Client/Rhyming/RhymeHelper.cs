using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Skinnix.Dictionaries;
using Skinnix.Dictionaries.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Client.Rhyming;

public class RhymeHelper
{
	public const int MAX_RESULTS = 300;

	private readonly RhymeWordList rhymeWordList;
	//private readonly SpellingList spellingList;
	private readonly WordFormList wordFormList;
	private readonly ComparisonWordList comparisonWordList;

	public RhymeHelper(RhymeWordList rhymeWordList, WordFormList wordFormList, ComparisonWordList comparisonWordList)
	{
		this.rhymeWordList = rhymeWordList;
		this.wordFormList = wordFormList;
		this.comparisonWordList = comparisonWordList;
	}

	public WordBase? Find(string word, int maxRhymeSyllables = 3)
	{
		var result = rhymeWordList.FindWord(word);
		if (!result.Success)
			return null;

		return new SimpleWord(this, result, maxRhymeSyllables);
	}

	public IEnumerable<WordBase> FindAll(string word, int maxRhymeSyllables = 3)
	{
		//Suche die Worte
		var words = rhymeWordList.FindAllWords(word).ToArray();

		//Finde den Wortstamm
		var stems = wordFormList.FindAllForms(word).Select(r => r.Stem).Where(s => s.HasValue).Select(s => s!.Value).Distinct().ToArray();
		if (stems.Length != 0)
		{
			//Suche die Wortstämme in der Vergleichsliste
			var candidateFound = false;
			RhymeWordList.Result stemWord = default;
			ComparisonWordList.Result comparisonWord = default;
			foreach (var stem in stems)
			{
				//Wenn der Stamm identisch zum Wort ist, überspringe
				if (stem.Form == word)
					continue;

				if (!candidateFound)
				{
					//Suche den Stamm in der Wortliste
					stemWord = rhymeWordList.FindWord(stem.Form);
					if (!stemWord.Success)
						break;

					//Suche das Wort in der Vergleichsliste
					comparisonWord = comparisonWordList.FindWord(word);
					if (!comparisonWord.Success)
						break;

					candidateFound = true;
				}

				//Suche den Stamm in der Vergleichsliste
				var comparisonStem = comparisonWordList.FindWord(stem.Form);
				if (comparisonStem.Success)
				{
					//Prüfe alle möglichen Aussprachen
					foreach (var wordIpa in comparisonWord.Ipas)
					{
						foreach (var stemIpa in comparisonStem.Ipas)
						{
							//Finde Stem in Word
							var match = IpaReplacer.FindBestMatch(wordIpa, stemIpa, false, false, tolerance: 3, preferShorter: true);
							if (match.index >= 0 && match.levenshtein <= 3)
							{
								//Matche die Stämme
								var comparisonMatch = IpaReplacer.FindBestMatch(stemWord.Ipa, stemIpa[0..match.length], true, false, preferShorter: true);
								if (comparisonMatch.index >= 0)
								{
									//Ersetze die IPA des Stamms im ComparisonWord durch die des ursprünglichen Wortstamms
									var cutIpa = stemWord.Ipa[comparisonMatch.index..(comparisonMatch.index + comparisonMatch.length)];
									var newIpa = wordIpa[0..match.index] + cutIpa + wordIpa[(match.index + match.length)..];
									if (newIpa == wordIpa)
										continue;

									yield return new InflectedWord(this, word, newIpa, maxRhymeSyllables);
								}
							}
						}
					}
				}
			}
		}

		foreach (var w in words)
			yield return new SimpleWord(this, w, maxRhymeSyllables);
	}

	public WordGroup<SimpleWord> FindBySuffix(string suffix, int maxRhymeSyllables = 3)
	{
		var results = rhymeWordList.FindBySuffix(suffix)
			.OrderByDescending(r => r.Frequency)
			.Select(g => new SimpleWord(this, g, maxRhymeSyllables))
			.Take(MAX_RESULTS)
			.ToArray();

		return new(suffix, results);
	}

	public abstract record WordBase(RhymeHelper Helper, string Word, string Ipa, int MaxRhymeSyllables)
	{
		private int stressedSyllableIndexFromEnd = -1;

		public int RhymeSyllables
			=> stressedSyllableIndexFromEnd != -1 ? stressedSyllableIndexFromEnd
			: stressedSyllableIndexFromEnd = IpaHelper.GetRhymeSuffixArray(Ipa, MaxRhymeSyllables).Length;

		public RhymeSearchResult FindRhymes(int maxSyllables = 3)
		{
			var ipaSuffix = IpaHelper.GetRhymeSuffixArray(Ipa, maxSyllables);
			var ipaSuffixLength = ipaSuffix.Length;
			if (ipaSuffixLength != 0 && !ipaSuffix[0].Any(IpaHelper.IsVowel))
				ipaSuffixLength--;

			var bestSuffixLength = IpaHelper.GetRhymeSuffixArray(Ipa, maxSyllables).Length;
			var rhymeGroups = new WordGroup<SimpleWord>?[ipaSuffixLength];
			var composite = new List<RhymeWordList.Result>();
			for (var i = ipaSuffixLength; i >= 1; i--)
			{
				var suffix = string.Join(null, ipaSuffix.TakeLast(i));
				var results = new List<RhymeWordList.Result>();
				foreach (var result in Helper.rhymeWordList.EnumerateRhymes(Ipa, i).Take(MAX_RESULTS))
				{
					if (rhymeGroups.Any(g => g?.IsEmpty == false && g.Results.Any(r => r.WordResult == result)))
						continue;

					if (composite.Contains(result))
						continue;

					if (result.Word.EndsWith(Word, StringComparison.InvariantCultureIgnoreCase)
						&& result.Ipa.EndsWith(Ipa, StringComparison.OrdinalIgnoreCase))
						composite.Add(result);
					else
						results.Add(result);
				}

				rhymeGroups[i - 1] = Group(Helper, $"/{suffix}/", results, MaxRhymeSyllables, i == bestSuffixLength);
			}

			var compositeGroup = Group(Helper, "-" + Word.ToLowerFirst(), composite, MaxRhymeSyllables, false);

			return new RhymeSearchResult([this], rhymeGroups!, compositeGroup.IsEmpty ? [] : [compositeGroup]);
		}

		private static WordGroup<SimpleWord> Group(RhymeHelper helper, string term, List<RhymeWordList.Result> results, int maxRhymeSyllables, bool favorite)
		{
			var words = results
				.OrderByDescending(r => r.Frequency)
				.Select(g => new SimpleWord(helper, g, maxRhymeSyllables))
				.ToArray();
			return new(term, words)
			{
				Favorite = favorite,
			};
		}

		private static WordGroup<WordWithExtensions> GroupWithExtensions(RhymeHelper helper, string suffix, List<RhymeWordList.Result> results, int maxRhymeSyllables, bool favorite)
		{
			var groupedResults = new List<(RhymeWordList.Result Word, List<RhymeWordList.Result> ExtensionWords)>();
			while (results.Count != 0)
			{
				var result = results[^1];
				results.RemoveAt(results.Count - 1);

				var extensionWords = new List<RhymeWordList.Result>();
				results.RemoveAll(r =>
				{
					if (r.Word.EndsWith(result.Word, StringComparison.InvariantCultureIgnoreCase)
						&& r.Frequency <= result.Frequency)
					{
						extensionWords.Add(r);
						return true;
					}

					return false;
				});
				groupedResults.RemoveAll(g =>
				{
					if (g.Word.Word.EndsWith(result.Word, StringComparison.InvariantCultureIgnoreCase)
						&& g.Word.Frequency <= result.Frequency)
					{
						extensionWords.Add(g.Word);
						extensionWords.AddRange(g.ExtensionWords);
						return true;
					}

					return false;
				});

				groupedResults.Add((result, extensionWords));
			}

			var groups = groupedResults
				.OrderByDescending(r => r.Word.Frequency)
				.Select(g => new WordWithExtensions(helper, g.Word, maxRhymeSyllables, g.ExtensionWords.OrderByDescending(w => w.Frequency).ToArray()))
				.ToArray();
			return new(suffix, groups)
			{
				Favorite = favorite,
			};
		}
	}

	public sealed record InflectedWord(RhymeHelper Helper, string Word, string Ipa, int MaxRhymeSyllables) :
		WordBase(Helper, Word, Ipa, MaxRhymeSyllables);

	public sealed record SimpleWord(RhymeHelper Helper, RhymeWordList.Result WordResult, int MaxRhymeSyllables) :
		WordBase(Helper, WordResult.Word, WordResult.Ipa, MaxRhymeSyllables);

	public record WordWithExtensions(RhymeHelper Helper, RhymeWordList.Result WordResult, int MaxRhymeSyllables, IReadOnlyCollection<RhymeWordList.Result> ExtensionWords) :
		WordBase(Helper, WordResult.Word, WordResult.Ipa, MaxRhymeSyllables)
	{
		public bool IsOrContains(RhymeWordList.Result result) => WordResult == result || ExtensionWords.Contains(result);
	}

	public record WordGroup<TWord>(string Term, IReadOnlyCollection<TWord> Results)
		where TWord : WordBase
	{
		public bool IsEmpty => Term is null || Results is null || Results.Count == 0;

		public bool Favorite { get; init; }
	}

	public record RhymeSearchResult(IReadOnlyCollection<WordBase> Words,
		IReadOnlyList<WordGroup<SimpleWord>> SyllableRhymes,
		IReadOnlyList<WordGroup<SimpleWord>> WordExtensions)
	{
		public static RhymeSearchResult Merge(IEnumerable<RhymeSearchResult> results)
		{
			var words = new List<WordBase>();
			var syllableRhymes = new List<(string Term, List<SimpleWord> Results, List<WordGroup<SimpleWord>> Favorites)>();
			var wordExtensions = new List<(string Term, List<SimpleWord> Results, List<WordGroup<SimpleWord>> Favorites)>();
			foreach (var result in results)
			{
				foreach (var word in result.Words)
					if (!words.Contains(word))
						words.Add(word);

				foreach (var group in result.SyllableRhymes)
				{
					var existing = syllableRhymes.FirstOrDefault(g => g.Term == group.Term);
					if (existing.Term is null)
						syllableRhymes.Add(existing = (group.Term, new List<SimpleWord>(group.Results), new()));
					else
					{
						foreach (var r in group.Results)
							if (existing.Results.Count < MAX_RESULTS && !existing.Results.Contains(r))
								existing.Results.Add(r);
					}

					if (group.Favorite)
						existing.Favorites.Add(group);
				}

				foreach (var group in result.WordExtensions)
				{
					var existing = wordExtensions.FirstOrDefault(g => g.Term == group.Term);
					if (existing.Term is null)
						wordExtensions.Add(existing = (group.Term, new List<SimpleWord>(group.Results), new()));
					else
					{
						foreach (var r in group.Results)
							if (existing.Results.Count < MAX_RESULTS && !existing.Results.Contains(r))
								existing.Results.Add(r);
					}

					if (group.Favorite)
						existing.Favorites.Add(group);
				}
			}

			return new(words,
				syllableRhymes.Select(g => new WordGroup<SimpleWord>(g.Term, g.Results) { Favorite = g.Favorites.Count != 0 }).ToArray(),
				wordExtensions.Select(g => new WordGroup<SimpleWord>(g.Term, g.Results) { Favorite = g.Favorites.Count != 0 }).ToArray());
		}
	}
}
