using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

using SeparatorStorageNumber = System.UInt64; //Längstes Wort: 58 Zeichen
using SyllableStorageNumber = System.UInt16; //Maximal 16 Silben

namespace ScraperBase;

public class RhymeList : IReadOnlyList<RhymeList.Result>
{
	private const char RHYME_SEPARATOR = '¦';
	private const SeparatorStorageNumber IGNORE_LAST_COMPONENT = (SeparatorStorageNumber.MaxValue >> 1) + 1;

	private readonly Entry[] entries;
	private readonly int[][] ipaRhymeGroups;
	private readonly SyllableOptions<int[][]> suffixRhymeGroups;

	public int Count => entries.Length;
	public Result this[int index] => new(this, entries[index]);

	private RhymeList(SortedSet<Builder.Entry> entries, SortedSet<Builder.TextGroup> ipaRhymes, SyllableOptions<SortedSet<Builder.TextGroup>> suffixRhymes)
	{
		this.ipaRhymeGroups = new int[ipaRhymes.Count][];
		var i = 0;
		foreach (var ipaGroup in ipaRhymes)
			this.ipaRhymeGroups[i++] = ipaGroup.EntryIndexes.ToArray();

		this.suffixRhymeGroups = new(new int[suffixRhymes.One.Count][], new int[suffixRhymes.Two.Count][], new int[suffixRhymes.Three.Count][]);
		foreach ((var syllableGroup, var thisSyllableGroup) in suffixRhymes.All.Zip(this.suffixRhymeGroups.All))
		{
			i = 0;
			foreach (var suffixGroup in syllableGroup)
				thisSyllableGroup[i++] = suffixGroup.EntryIndexes.ToArray();
		}

		this.entries = new Entry[entries.Count];
		i = 0;
		var stressInfo = 0;
		foreach (var builderEntry in entries)
		{
			if (builderEntry.Suffix is not null)
			{
				var suffixIndex = builderEntry.Suffix.Index;
				this.entries[i++] = new Entry(builderEntry.Text, builderEntry.Syllables?.FirstOrDefault() ?? default, suffixIndex);
			}
			else
			{
				var entryIpaRhymes = builderEntry.IpaRhymeGroups.Select(g => g.Index).ToArray();
				var entrySuffixRhymes = new SyllableOptions<int[]>(
					builderEntry.SuffixRhymeGroups.One.Select(g => g.Index).ToArray(),
					builderEntry.SuffixRhymeGroups.Two.Select(g => g.Index).ToArray(),
					builderEntry.SuffixRhymeGroups.Three.Select(g => g.Index).ToArray());

				this.entries[i++] = new Entry(builderEntry.Text, builderEntry.Syllables?.FirstOrDefault() ?? default, entryIpaRhymes, entrySuffixRhymes, builderEntry.StressedSyllables);

				if (builderEntry.StressedSyllables != default)
					stressInfo++;
			}
		}

		Console.WriteLine(stressInfo);
	}

	public Result? Find(string word)
	{
		var index = Array.BinarySearch(entries, Entry.CreateSearchEntry(word));
		return index < 0 ? null : new(this, entries[index]);
	}

	public IEnumerator<Result> GetEnumerator()
	{
		foreach (var entry in entries)
			yield return new(this, entry);
	}

	IEnumerator IEnumerable.GetEnumerator() => entries.GetEnumerator();

	private static SeparatorStorageNumber CombinePositions(IEnumerable<byte> positions)
	{
		SeparatorStorageNumber result = 0;
		foreach (var position in positions)
		{
			if (position == byte.MaxValue)
				result |= IGNORE_LAST_COMPONENT;
			else
				result |= 1ul << position;
		}

		return result;
	}

	private static IEnumerable<string> EnumerateWord(string word, SeparatorStorageNumber parts)
	{
		var last = 0;
		for (var i = 1; i < word.Length; i++)
		{
			parts >>= 1;
			if ((parts & 1ul) == 0)
				continue;

			yield return word[last..i];
			last = i;
		}

		if (last < word.Length)
			yield return word[last..];
	}

	internal readonly record struct SyllableOptions<T>(T One, T Two, T Three)
	{
		public T this[int index] => index switch
		{
			1 => One,
			2 => Two,
			_ => Three,
		};

	public IEnumerable<T> All
		{
			get
			{
				yield return One;
				yield return Two;
				yield return Three;
			}
		}
	}

	public readonly struct Result
	{
		private readonly RhymeList owner;
		private readonly Entry entry;

		public Word Word => new(entry);

		internal Result(RhymeList owner, Entry entry)
		{
			this.owner = owner;
			this.entry = entry;
		}

		public IEnumerable<Word> GetIpaRhymes()
		{
			foreach (var group in entry.GetIpaRhymeGroups())
				foreach (var rhymeIndex in owner.ipaRhymeGroups[group])
					yield return new(owner.entries[rhymeIndex]);
		}

		public IEnumerable<Word> GetSuffixRhymes(int syllableCount)
		{
			foreach (var group in entry.GetSuffixRhymeGroups(syllableCount))
				foreach (var rhymeIndex in owner.suffixRhymeGroups[syllableCount][group])
					yield return new(owner.entries[rhymeIndex]);
		}
	}

	public readonly record struct Word(string Text, SeparatorStorageNumber Syllables)
	{
		internal Word(Entry entry)
			: this(entry.Text, entry.Syllables)
		{ }

		public Hyphenation GetHyphenation()
		{
			var positions = new List<byte>();
			var last = 0;
			for (var i = 0; i < Text.Length; i++)
			{
				if ((Syllables & (1ul << i)) == 0)
					continue;
				positions.Add((byte)(i - last));
				last = i;
			}
			return new Hyphenation(positions.ToArray());
		}

		public IEnumerable<string> Hyphenate()
			=> Syllables == 0 ? [Text]
			: EnumerateWord(Text, Syllables);

		public override string ToString() => Text;
	}

	internal readonly struct Entry : IComparable<Entry>
	{
		private readonly string text;
		private readonly SeparatorStorageNumber syllables;
		private readonly int suffixIndex;
		private readonly int[]? ipaRhymeGroups;
		private readonly SyllableOptions<int[]?> suffixRhymeGroups;
		private readonly SyllableStorageNumber stressedSyllables;

		public string Text => text;
		public SeparatorStorageNumber Syllables => syllables;
		public int SuffixIndex => suffixIndex;

		internal Entry(string word, SeparatorStorageNumber syllables, int suffixIndex)
		{
			this.text = word;
			this.syllables = syllables;
			this.suffixIndex = suffixIndex;
		}

		internal Entry(string word, SeparatorStorageNumber syllables, int[] ipaRhymes, SyllableOptions<int[]> suffixRhymes, SyllableStorageNumber stressedSyllables)
		{
			this.text = word;
			this.syllables = syllables;
			this.suffixIndex = -1;
			this.stressedSyllables = stressedSyllables;

			this.ipaRhymeGroups = ipaRhymes.OrNullIfEmpty();
			this.suffixRhymeGroups = new(
				suffixRhymes.One.OrNullIfEmpty(),
				suffixRhymes.Two.OrNullIfEmpty(),
				suffixRhymes.Three.OrNullIfEmpty());
		}

		internal static Entry CreateSearchEntry(string word) => new(word, 0, -1);

		public IEnumerable<int> GetIpaRhymeGroups()
		{
			if (ipaRhymeGroups is null)
				yield break;
			foreach (var group in ipaRhymeGroups)
				yield return group;
		}

		public IEnumerable<int> GetSuffixRhymeGroups(int syllableCount)
		{
			switch (syllableCount)
			{
				case 1:
					if (suffixRhymeGroups.One is null)
						yield break;
					foreach (var group in suffixRhymeGroups.One)
						yield return group;
					break;
				case 2:
					if (suffixRhymeGroups.Two is null)
						yield break;
					foreach (var group in suffixRhymeGroups.Two)
						yield return group;
					break;
				default:
					if (suffixRhymeGroups.Three is null)
						yield break;
					foreach (var group in suffixRhymeGroups.Three)
						yield return group;
					break;
			}
		}

		public int CompareTo(Entry other)
			=> string.Compare(text, other.text, StringComparison.InvariantCultureIgnoreCase);
	}

	public class Builder
	{
		private const string ALLOWED_CHARACTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜabcdefghijklmnopqrstuvwxyzäöüß-ÁÀÅÉÈÍÌÓÒÚÙáàéèíìóòúùçş₂ñ";
		private const string ALLOWED_SPECIAL_CHARACTERS = "-₂’.";
		private const string BLACKLIST_CHARACTERS = "0123456789αβ%";

		private readonly SortedSet<Entry> entries = new();
		private readonly Dictionary<string, List<WordInfo.WordForm>> extendedForms = new();

		private int maxLength;
		private int maxSyllables;
		private int maxRhymes;

		public void Add(WordInfo word)
		{
			if (word.BaseForm is not null)
			{
				if (!extendedForms.TryGetValue(word.BaseForm, out var list))
					extendedForms.Add(word.BaseForm, list = new());

				list.AddRange(word.Forms);
				return;
			}

			foreach (var form in word.Forms)
				Add(word, form);
		}

		private void Add(WordInfo word, WordInfo.WordForm form)
		{
			if (form.Text.Contains(' '))
			{
				return;
			}

			if (!form.Text.All(c => char.IsLetter(c) || ALLOWED_SPECIAL_CHARACTERS.Contains(c)))
			{
				if (form.Hyphenations.Count != 0 || form.Rhymes.Count != 0 || form.Components.Count != 0)
				{
					if (!form.Text.All(c => char.IsLetter(c) || ALLOWED_SPECIAL_CHARACTERS.Contains(c) || BLACKLIST_CHARACTERS.Contains(c)))
					{
						Console.WriteLine("Verwerfe : " + form.Text);
					}
				}
				return;
			}

			if (form.Text.Contains("Rose"))
			{

			}

			var rhymes = form.Rhymes.SelectMany(r => r.Values).Select(IpaHelper.GetRhymeSyllable).Where(s => s != string.Empty).Distinct();
			var components = form.Components;
			var hyphenations = form.Hyphenations.Select(h => new Hyphenation(h)).ToArray();

			var entry = new Entry(form.Text, hyphenations, rhymes, components);
			foreach (var ipa in form.Rhymes.SelectMany(r => r.Values).Distinct())
			{
				var ipaSyllables = IpaHelper.SplitSyllables(ipa).ToArray();
				if (ipa.Length == 1 && hyphenations.Length == 0)
				{
					entry.StressedSyllables |= 1;
				}
				else if (hyphenations.All(h => h.Positions.Length == ipaSyllables.Length - 1))
				{
					SyllableStorageNumber modifier = 1;
					foreach (var syllable in ipaSyllables)
					{
						if (IpaHelper.IsStressMarker(syllable[0]))
							entry.StressedSyllables |= modifier;

						modifier <<= 1;
					}
				}
				else
				{

				}
			}
			entries.Add(entry);

			if (entry.Text.Length > maxLength)
				maxLength = entry.Text.Length;
			if (entry.Syllables?.Length > maxSyllables)
				maxSyllables = entry.Syllables.Length;
			if (entry.Rhymes.Length > maxRhymes)
				maxRhymes = entry.Rhymes.Length;
		}

		public RhymeList Build()
		{
			//Erweitere Formen
			var extendedFormsAdded = 0;
			foreach (var extendedPair in extendedForms)
			{
				if (entries.TryGetValue(Entry.CreateSearchEntry(extendedPair.Key), out var baseEntry))
				{
					foreach (var form in extendedPair.Value)
					{
						if (form.Hyphenations.Count == 0 && form.Rhymes.Count == 0 && form.Components.Count == 0)
							continue;

						if (entries.TryGetValue(Entry.CreateSearchEntry(form.Text), out var existing))
							continue;

						var rhymes = form.Rhymes.SelectMany(r => r.Values).Distinct();
						var components = form.Components;
						var hyphenations = form.Hyphenations.Select(h => new Hyphenation(h)).ToArray();
						var newEntry = new Entry(form.Text, hyphenations, rhymes, components);
						extendedFormsAdded++;
					}
				}
			}
			extendedForms.Clear();

			//Erstelle Reimgruppen
			var ipaRhymes = new SortedSet<TextGroup>();
			var suffixRhymes = new SyllableOptions<SortedSet<TextGroup>>(new(), new(), new());
			var composites = new List<Entry>();
			var index = 0;
			var maxIpaGroups = 0;
			var maxSuffixGroups1 = 0;
			var maxSuffixGroups2 = 0;
			var maxSuffixGroups3 = 0;
			foreach (var entry in entries)
			{
				entry.Index = index++;

				foreach (var rhyme in entry.Rhymes)
				{
					if (!ipaRhymes.TryGetValue(new TextGroup(rhyme), out var ipaGroup))
						ipaRhymes.Add(ipaGroup = new TextGroup(rhyme));

					if (ipaGroup.Add(entry.Index))
						entry.IpaRhymeGroups.Add(ipaGroup);
				}

				var components = entry.GetComponents();
				if (components is not null)
				{
					var lastComponent = (entry.Components & IGNORE_LAST_COMPONENT) != 0
						? string.Join(null, components.TakeLast(2))
						: components.Last();

					if (!lastComponent.Equals(entry.Text, StringComparison.InvariantCultureIgnoreCase)
						&& entries.TryGetValue(Entry.CreateSearchEntry(lastComponent), out var suffix))
					{
						entry.Suffix = suffix;
						composites.Add(entry);
						continue;
					}
				}

				if (entry.Text == "Rose")
				{ }

				var rhymes = GetRhymeSuffixes(entry);
				foreach (var suffixRhyme in rhymes.One)
				{
					if (!suffixRhymes.One.TryGetValue(new TextGroup(suffixRhyme), out var suffixGroup))
						suffixRhymes.One.Add(suffixGroup = new TextGroup(suffixRhyme));

					if (suffixGroup.Add(entry.Index))
						entry.SuffixRhymeGroups.One.Add(suffixGroup);
				}

				foreach (var suffixRhyme in rhymes.Two)
				{
					if (!suffixRhymes.Two.TryGetValue(new TextGroup(suffixRhyme), out var suffixGroup))
						suffixRhymes.Two.Add(suffixGroup = new TextGroup(suffixRhyme));

					if (suffixGroup.Add(entry.Index))
						entry.SuffixRhymeGroups.Two.Add(suffixGroup);
				}

				foreach (var suffixRhyme in rhymes.Three)
				{
					if (!suffixRhymes.Three.TryGetValue(new TextGroup(suffixRhyme), out var suffixGroup))
						suffixRhymes.Three.Add(suffixGroup = new TextGroup(suffixRhyme));

					if (suffixGroup.Add(entry.Index))
						entry.SuffixRhymeGroups.Three.Add(suffixGroup);
				}

				if (entry.IpaRhymeGroups.Count > maxIpaGroups)
					maxIpaGroups = entry.IpaRhymeGroups.Count;
				if (entry.SuffixRhymeGroups.One.Count > maxSuffixGroups1)
					maxSuffixGroups1 = entry.SuffixRhymeGroups.One.Count;
				if (entry.SuffixRhymeGroups.Two.Count > maxSuffixGroups2)
					maxSuffixGroups2 = entry.SuffixRhymeGroups.Two.Count;
				if (entry.SuffixRhymeGroups.Three.Count > maxSuffixGroups3)
					maxSuffixGroups3 = entry.SuffixRhymeGroups.Three.Count;
			}

			//Verknüpfe Komposita
			foreach (var entry in composites)
			{
				//Verwende letztes Suffix
				var suffix = entry.Suffix!;
				while (suffix.Suffix is not null)
					suffix = suffix.Suffix;

				entry.Suffix = suffix;
			}
			composites.Clear();

			//Entferne Reimgruppen, die nur ein Element haben
			var unusableRhymes = ipaRhymes.RemoveWhere(g => g.EntryIndexes.Count <= 1);
			foreach (var syllableGroup in suffixRhymes.All)
				unusableRhymes += syllableGroup.RemoveWhere(g => g.EntryIndexes.Count <= 1);

			//Indiziere Reimgruppen
			index = 0;
			foreach (var group in ipaRhymes)
				group.Index = index++;
			foreach (var syllableGroup in suffixRhymes.All)
			{
				index = 0;
				foreach (var group in syllableGroup)
					group.Index = index++;
			}

			return new RhymeList(entries, ipaRhymes, suffixRhymes);
		}

		private SyllableOptions<SortedSet<string>> GetRhymeSuffixes(Entry entry)
		{
			var syllables = entry.GetSyllables();
			if (syllables is null)
				return new([RhymeHelper.GetRhymableSuffix(entry.Text)], [], []);

			var result = new SyllableOptions<SortedSet<string>>([], [], []);
			foreach (var syllable in syllables)
			{
				var suffix = string.Empty;
				var previous = string.Empty;
				foreach (var s in syllable.TakeLast(3).Reverse().Select((s, i) => (Item: s, Index: i)))
				{
					suffix = s.Item + suffix;
					var rhyme = RhymeHelper.GetRhymableSuffix(suffix);
					if (rhyme == previous)
						continue;
					switch (s.Index)
					{
						case 0:
							result.One.Add(rhyme);
							break;
						case 1:
							result.Two.Add(rhyme);
							break;
						default:
							result.Three.Add(rhyme);
							break;
					}
				}
			}

			return result;
		}

		internal class Entry : IComparable<Entry>
		{
			private readonly string text;
			private readonly string rhymes;
			private readonly SeparatorStorageNumber[]? syllables;
			private readonly SeparatorStorageNumber components;

			public string Text => text;
			public string[] Rhymes => rhymes.Split(RHYME_SEPARATOR, StringSplitOptions.RemoveEmptyEntries);

			public SeparatorStorageNumber[]? Syllables => syllables;
			public SeparatorStorageNumber Components => components;

			public List<TextGroup> IpaRhymeGroups { get; } = new();
			public SyllableOptions<List<TextGroup>> SuffixRhymeGroups { get; } = new(new(), new(), new());

			public SyllableStorageNumber StressedSyllables { get; set; }

			public int Index { get; set; } = -1;
			public Entry? Suffix { get; set; }

			public Entry(string word, IReadOnlyList<Hyphenation> hyphenations, IEnumerable<string> rhymes, IEnumerable<string> components)
			{
				this.text = word;
				this.rhymes = string.Join(RHYME_SEPARATOR, rhymes);

				this.syllables = hyphenations.Select(h => h.Positions.Length switch
				{
					<= 3 => CombinePositions(h.Positions),
					_ => CombinePositions(h.Positions[(h.Positions.Length - 4)..])
				}).ToArray();
				if (this.syllables.Length == 0)
					this.syllables = null;

				this.components = CombinePositions(FindComponents(word, components.Where(c => c != word)));
			}

			public static Entry CreateSearchEntry(string word)
				=> new(word, [], [], []);

			internal static IEnumerable<byte> FindComponents(string word, IEnumerable<string> components)
			{
				var lastIndex = 0;
				var end = 0;
				var any = false;
				foreach (var component in components)
				{
					var index = WordComponentComparer.Instance.IndexOf(word, component, lastIndex);
					if (index >= 0)
					{
						any = true;
						yield return (byte)index;
						lastIndex = index;
						var componentEnd = index + component.Length;
						if (componentEnd > end)
							end = componentEnd;
						continue;
					}

					index = WordComponentComparer.Instance.IndexOf(word, component);
					if (index >= 0)
					{
						any = true;
						yield return (byte)index;
						var componentEnd = index + component.Length;
						if (componentEnd > end)
							end = componentEnd;
						continue;
					}

					continue;
				}

				if (any && end < word.Length)
				{
					yield return (byte)end;
					yield return byte.MaxValue;
				}
			}

			public IEnumerable<string>? GetComponents()
			{
				if (components == 0)
					return null;

				return EnumerateWord(text, components);
			}

			public IEnumerable<IEnumerable<string>>? GetSyllables()
			{
				return Syllables?.Select(s => EnumerateWord(text, s));
			}

			public int CompareTo(Entry? other)
				=> other is null ? 1
				: string.Compare(text, other.text, StringComparison.InvariantCultureIgnoreCase);
		}

		internal class TextGroup(string text) : IComparable<TextGroup>
		{
			public string Text => text;
			public SortedSet<int> EntryIndexes { get; } = new();

			public int Index { get; set; } = -1;

			public bool Add(int entryIndex)
				=> EntryIndexes.Add(entryIndex);

			public int CompareTo(TextGroup? other)
				=> other is null ? 1
				: string.Compare(text, other.Text, StringComparison.InvariantCultureIgnoreCase);
		}
	}

	private class WordComponentComparer : IComparer<char>, IComparer<string>
	{
		public static readonly WordComponentComparer Instance = new();

		public int IndexOf(string text, string value, int startIndex = 0)
		{
			var length = value.Length;
			var max = text.Length - length;
			var textSpan = text.AsSpan();
			var valueSpan = value.AsSpan();
			for (var i = startIndex; i <= max; i++)
			{
				if (Compare(textSpan.Slice(i, value.Length), valueSpan) == 0)
					return i;
			}

			return -1;
		}

		public int Compare(string? x, string? y)
		{
			if (x is null)
				return y is null ? 0 : -1;
			if (y is null)
				return 1;

			var min = Math.Min(x.Length, y.Length);
			for (var i = 0; i < min; i++)
			{
				var result = Compare(x[i], y[i]);
				if (result != 0)
					return result;
			}

			return x.Length - y.Length;
		}

		public int Compare(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
		{
			var min = Math.Min(x.Length, y.Length);
			for (var i = 0; i < min; i++)
			{
				var result = Compare(x[i], y[i]);
				if (result != 0)
					return result;
			}

			return x.Length - y.Length;
		}

		public int Compare(char x, char y)
		{
			if (!char.IsLetter(x))
				return !char.IsLetter(y) ? 0 : -1;
			else if (!char.IsLetter(y))
				return 1;

			return char.ToLowerInvariant(x).CompareTo(char.ToLowerInvariant(y));
		}
	}
}
