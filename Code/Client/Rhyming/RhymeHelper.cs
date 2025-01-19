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

	private readonly WordList wordList;

	public RhymeHelper(WordList wordList)
	{
		this.wordList = wordList;
	}

	public Word? Find(string word, int maxRhymeSyllables = 3)
	{
		var result = wordList.FindWord(word);
		if (result.IsEmpty)
			return null;

		return new SimpleWord(result, maxRhymeSyllables);
	}

	public IEnumerable<Word> FindAll(string word, int maxRhymeSyllables = 3)
		=> wordList.FindAllWords(word)
			.Select(r => new SimpleWord(r, maxRhymeSyllables));

	public WordGroup<SimpleWord> FindBySuffix(string suffix, int maxRhymeSyllables = 3)
	{
		var results = wordList.FindBySuffix(suffix)
			.OrderByDescending(r => r.AdditionalData.Frequency)
			.Select(g => new SimpleWord(g, maxRhymeSyllables))
			.Take(MAX_RESULTS)
			.ToArray();

		return new(suffix, results);
	}

	public abstract record Word(WordList.Result WordResult, int MaxRhymeSyllables)
	{
		private int stressedSyllableIndexFromEnd = -1;

		public int RhymeSyllables
			=> stressedSyllableIndexFromEnd != -1 ? stressedSyllableIndexFromEnd
			: stressedSyllableIndexFromEnd = IpaHelper.GetRhymeSuffixArray(WordResult.Ipa, MaxRhymeSyllables).Length;

		public RhymeSearchResult FindRhymes(int maxSyllables = 3)
		{
			var word = WordResult.Word;
			var ipa = WordResult.Ipa;
			var ipaSuffix = IpaHelper.GetRhymeSuffixArray(WordResult.Ipa, maxSyllables);
			var ipaSuffixLength = ipaSuffix.Length;
			if (ipaSuffixLength != 0 && !ipaSuffix[0].Any(IpaHelper.IsVowel))
				ipaSuffixLength--;

			var bestSuffixLength = IpaHelper.GetRhymeSuffixArray(ipa, maxSyllables).Length;
			var rhymeGroups = new WordGroup<SimpleWord>?[ipaSuffixLength];
			var composite = new List<IpaRhymeList<RhymeListWordData>.Result>();
			for (var i = ipaSuffixLength; i >= 1; i--)
			{
				var suffix = string.Join(null, ipaSuffix.TakeLast(i));
				var results = new List<IpaRhymeList<RhymeListWordData>.Result>();
				foreach (var result in WordResult.EnumerateGroup(i).Take(MAX_RESULTS))
				{
					if (rhymeGroups.Any(g => g?.IsEmpty == false && g.Results.Any(r => r.WordResult == result)))
						continue;

					if (composite.Contains(result))
						continue;

					if (result.Word.EndsWith(word, StringComparison.InvariantCultureIgnoreCase)
						&& result.Ipa.EndsWith(ipa, StringComparison.OrdinalIgnoreCase))
						composite.Add(result);
					else
						results.Add(result);
				}

				rhymeGroups[i - 1] = Group($"/{suffix}/", results, MaxRhymeSyllables, i == bestSuffixLength);
			}

			var compositeGroup = Group("-" + word.ToLowerFirst(), composite, MaxRhymeSyllables, false);

			return new RhymeSearchResult([this], rhymeGroups!, compositeGroup.IsEmpty ? [] : [compositeGroup]);
		}

		private static WordGroup<SimpleWord> Group(string term, List<WordList.Result> results, int maxRhymeSyllables, bool favorite)
		{
			var words = results
				.OrderByDescending(r => r.AdditionalData.Frequency)
				.Select(g => new SimpleWord(g, maxRhymeSyllables))
				.ToArray();
			return new(term, words)
			{
				Favorite = favorite,
			};
		}

		private static WordGroup<WordWithExtensions> GroupWithExtensions(string suffix, List<WordList.Result> results, int maxRhymeSyllables, bool favorite)
		{
			var groupedResults = new List<(WordList.Result Word, List<WordList.Result> ExtensionWords)>();
			while (results.Count != 0)
			{
				var result = results[^1];
				results.RemoveAt(results.Count - 1);

				var extensionWords = new List<WordList.Result>();
				results.RemoveAll(r =>
				{
					if (r.Word.EndsWith(result.Word, StringComparison.InvariantCultureIgnoreCase)
						&& r.AdditionalData.Frequency <= result.AdditionalData.Frequency)
					{
						extensionWords.Add(r);
						return true;
					}

					return false;
				});
				groupedResults.RemoveAll(g =>
				{
					if (g.Word.Word.EndsWith(result.Word, StringComparison.InvariantCultureIgnoreCase)
						&& g.Word.AdditionalData.Frequency <= result.AdditionalData.Frequency)
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
				.OrderByDescending(r => r.Word.AdditionalData.Frequency)
				.Select(g => new WordWithExtensions(g.Word, maxRhymeSyllables, g.ExtensionWords.OrderByDescending(w => w.AdditionalData.Frequency).ToArray()))
				.ToArray();
			return new(suffix, groups)
			{
				Favorite = favorite,
			};
		}
	}

	public sealed record SimpleWord(WordList.Result WordResult, int MaxRhymeSyllables) : Word(WordResult, MaxRhymeSyllables);

	public record WordWithExtensions(WordList.Result WordResult, int MaxRhymeSyllables, IReadOnlyCollection<WordList.Result> ExtensionWords) :
		Word(WordResult, MaxRhymeSyllables)
	{
		public bool IsOrContains(WordList.Result result) => WordResult == result || ExtensionWords.Contains(result);
	}

	public record WordGroup<TWord>(string Term, IReadOnlyCollection<TWord> Results)
		where TWord : Word
	{
		public bool IsEmpty => Term is null || Results is null || Results.Count == 0;

		public bool Favorite { get; init; }
	}

	public record RhymeSearchResult(IReadOnlyCollection<Word> Words,
		IReadOnlyList<WordGroup<SimpleWord>> SyllableRhymes,
		IReadOnlyList<WordGroup<SimpleWord>> WordExtensions)
	{
		public static RhymeSearchResult Merge(IEnumerable<RhymeSearchResult> results)
		{
			var words = new List<Word>();
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
