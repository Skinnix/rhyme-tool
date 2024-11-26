using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ScraperBase;

public class RhymeTree
{
	private readonly WordEntry wordsRoot = new();
	private readonly RhymeEntry rhymesRoot = new();
	private SuffixEntry suffixRoot = new();

	public int Count { get; private set; }

	public void AddWord(WordInfo word)
	{
		foreach (var form in word.Forms)
			AddForm(word, form);
	}

	private Word? AddForm(WordInfo wordInfo, WordInfo.WordForm form)
	{
		var text = form.Text.ToLowerInvariant();
		if (text.IndexOfAny([' ', ',', ':']) >= 0)
			return null;

		const string ALLOWED_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜabcdefghijklmnopqrstuvwxyzäöüß";
		if (!text.All(ALLOWED_CHARS.Contains))
			return null;

		var entry = GetOrCreateEntry(wordsRoot, text);

		if (form.Suffix is not null)
		{
			var suffixEntry = GetOrCreateEntry(wordsRoot, form.Suffix.ToLowerInvariant());
			((List<WordEntry>)entry.SuffixWords).Add(suffixEntry);
		}

		var wordHyphenations = new HyphenationTarget[form.Hyphenations.Count];
		var i = 0;
		foreach (var positions in form.Hyphenations)
		{
			var hyphenation = new Hyphenation([..positions]);
			var syllables = hyphenation.GetSyllables(text);
			var suffixEntry = GetOrCreateEntry(suffixRoot, syllables.Last());
			((List<WordEntry>)suffixEntry.Words).Add(entry);

			wordHyphenations[i++] = new(hyphenation, suffixEntry);
		}

		var wordRhymes = new List<RhymeEntry>();
		var allRhymes = form.Rhymes.SelectMany(r => r.Values).Distinct();
		foreach (var rhyme in allRhymes)
		{
			var rhymeEntry = GetOrCreateEntry(rhymesRoot, rhyme);
			((List<WordEntry>)rhymeEntry.Words).Add(entry);

			wordRhymes.Add(rhymeEntry);
		}

		var word = new Word(wordHyphenations, wordRhymes.ToArray())
		{
			Popularity = wordInfo.Popularity,
			AntiPopularity = wordInfo.AntiPopularity,
		};
		((List<Word>)entry.Words).Add(word);
		Count++;
		return word;
	}

	public void Finish()
	{
		wordsRoot.Finish();
		rhymesRoot.Finish();
		suffixRoot = new();
	}

	public (IEnumerable<string> Rhymes, IEnumerable<string> HyphenRhymes) GetRhymes(string text)
	{
		var entry = TryGetEntry(wordsRoot, text);
		if (entry is null)
			return ([], []);

		var (rhymes, hyphenRhymes) = GetRhymes(entry, text);
		rhymes.Sort((w1, w2) => w2.Popularity - w1.Popularity);
		hyphenRhymes.Sort((w1, w2) => w2.Popularity - w1.Popularity);

		return (rhymes.Select(w => w.Word), hyphenRhymes.Select(w => w.Word));
	}

	private (List<(string Word, int Popularity)> Rhymes, List<(string Word, int Popularity)> HyphenRhymes) GetRhymes(WordEntry entry, string text)
	{
		var rhymes = new List<(string Word, int Popularity)>();
		foreach (var word in entry.Words)
			GetRhymes(rhymes, word, text);

		var hyphenRhymes = new List<(string Word, int Popularity)>();
		foreach (var word in entry.Words)
			GetHyphenRhymes(hyphenRhymes, word, text);

		foreach (var suffixWord in entry.SuffixWords)
		{
			var suffixText = suffixWord.ReconstructWord();
			var suffixRhymes = GetRhymes(suffixWord, suffixText);
			rhymes.AddRange(suffixRhymes.Rhymes);
			hyphenRhymes.AddRange(suffixRhymes.HyphenRhymes);
		}

		return (rhymes, hyphenRhymes);
	}

	private void GetRhymes(List<(string Word, int Popularity)> result, Word word, string text)
	{
		foreach (var rhyme in word.Rhymes)
		{
			foreach (var rhymeWord in rhyme.Words)
			{
				var rhymeText = rhymeWord.ReconstructWord();
				if (!rhymeText.Equals(text, StringComparison.OrdinalIgnoreCase) && !result.Any(r => r.Word == rhymeText))
					result.Add((rhymeText, rhymeWord.Words.Sum(w => w.AntiPopularity)));
			}
		}
	}

	private void GetHyphenRhymes(List<(string Word, int Popularity)> result, Word word, string text)
	{
		var targets = word.Hyphenations.SelectMany(h => h.Target.Words).Distinct();
		foreach (var target in targets)
		{
			var rhymeText = target.ReconstructWord();
			if (!rhymeText.Equals(text, StringComparison.OrdinalIgnoreCase) && !result.Any(r => r.Word == rhymeText))
				result.Add((rhymeText, target.Words.Sum(w => w.AntiPopularity)));
		}
	}

	private TEntry? TryGetEntry<TEntry>(TEntry entry, IEnumerable<char> text)
		where TEntry : Entry<TEntry>, new()
	{
		foreach (var character in text)
		{
			entry = entry.TryGetChild<TEntry>(character)!;
			if (entry is null)
				return null;
		}

		return entry;
	}

	private TEntry GetOrCreateEntry<TEntry>(TEntry entry, IEnumerable<char> text)
		where TEntry : Entry<TEntry>, new()
	{
		foreach (var character in text)
			entry = entry.GetOrCreateChild<TEntry>(character);

		return entry ?? throw new ArgumentException("Text erwartet");
	}

	private abstract record Entry<TEntry> : IComparable<TEntry>
		where TEntry : Entry<TEntry>
	{
		public char Character { get; init; }

		public abstract TEntry? Parent { get; init; }

		public IReadOnlyCollection<TEntry> Children { get; private set; } = new SortedSet<TEntry>();

		public SortedSet<TEntry>? EditChildren => Children as SortedSet<TEntry>;
		public TEntry[]? FinalChildren => Children as TEntry[];

		public int TotalCount { get; private set; }

		public int CompareTo(TEntry? other)
			=> other is null ? 1 : Character.CompareTo(other.Character);

		public TEntry? TryGetChild<TEntry1>(char character)
			where TEntry1 : TEntry, new()
		{
			if (EditChildren is not null)
			{
				var key = new TEntry1()
				{
					Character = character
				};
				return EditChildren.TryGetValue(key, out var entry) ? entry : null;
			}
			else
			{
				var index = Array.BinarySearch(FinalChildren!, new TEntry1()
				{
					Character = character
				});
				if (index < 0)
					return null;

				return FinalChildren![index];
			}
		}

		public TEntry GetOrCreateChild<TEntry1>(char character)
			where TEntry1 : TEntry, new()
		{
			if (EditChildren is not null)
			{
				var key = new TEntry1()
				{
					Character = character,
					Parent = (TEntry)this,
				};
				if (!EditChildren.TryGetValue(key, out var entry))
					EditChildren.Add(entry = key);

				return entry;
			}
			else
			{
				var index = Array.BinarySearch(FinalChildren!, new TEntry1()
				{
					Character = character
				});
				if (index < 0)
					throw new InvalidOperationException("Wort nicht gefunden");

				return FinalChildren![index];
			}
		}

		public virtual int Finish()
		{
			if (Children is SortedSet<TEntry> children)
				Children = children.ToArray();

			foreach (var child in Children)
				TotalCount += child.Finish();

			return TotalCount;
		}
	}

	private record WordEntry : Entry<WordEntry>
	{
		public override WordEntry? Parent { get; init; }

		public IReadOnlyCollection<Word> Words { get; private set; } = new List<Word>();
		public IReadOnlyCollection<WordEntry> SuffixWords { get; private set; } = new List<WordEntry>();

		public string ReconstructWord()
		{
			var builder = new StringBuilder();
			for (var entry = this; entry is not null; entry = entry.Parent)
				if (entry.Character != 0)
					builder.Insert(0, entry.Character);

			return builder.ToString();
		}

		public override int Finish()
		{
			base.Finish();

			if (Words is List<Word> words)
				Words = words.ToArray();

			if (SuffixWords is List<WordEntry> suffixWords)
				SuffixWords = suffixWords.ToArray();

			return TotalCount + Words.Count;
		}
	}

	private record RhymeEntry : Entry<RhymeEntry>
	{
		public override RhymeEntry? Parent { get => null; init { } }

		public IReadOnlyCollection<WordEntry> Words { get; private set; } = new List<WordEntry>();

		public override int Finish()
		{
			base.Finish();

			if (Words is List<WordEntry> words)
				Words = words.ToArray();

			return TotalCount + Words.Count;
		}
	}

	private record SuffixEntry : Entry<SuffixEntry>
	{
		public override SuffixEntry? Parent { get => null; init { } }

		public IReadOnlyCollection<WordEntry> Words { get; private set; } = new List<WordEntry>();

		public override int Finish()
		{
			base.Finish();

			if (Words is List<WordEntry> words)
				Words = words.ToArray();

			return TotalCount + Words.Count;
		}
	}

	private record Word(HyphenationTarget[] Hyphenations, RhymeEntry[] Rhymes)
	{
		public int Popularity { get; set; }
		public int AntiPopularity { get; set; }
	}

	private record HyphenationTarget(Hyphenation Hyphenation, SuffixEntry Target);

	private record Hyphenation(byte[] Positions)
	{
		public IEnumerable<string> GetSyllables(string text)
		{
			var previous = 0;
			foreach (var position in Positions)
			{
				var syllable = text[previous..position];
				yield return syllable;
				previous = position;
			}

			var lastSyllable = text[previous..];
			yield return lastSyllable;
		}
	}
}
