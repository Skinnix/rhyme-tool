using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WeCantSpell.Hunspell;

namespace Skinnix.RhymeTool.Rhyming;

public class RhymeWordList : WordFeatureList<RhymeWordList.Entry, RhymeWordList.Result>
{
	public const string IPA_IGNORE = "1ˈˌ§.̩̯̥̊̃̍̆͜͡";

	protected RhymeWordList(InstanceData data)
		: base(data)
	{ }

	protected override Result CreateResult(int index)
		=> new(this, index);

	public void Write(BinaryWriter writer)
		=> Write(writer, (writer, word) =>
		{
			writer.Write(word.Word);
			writer.Write(word.Ipa);
			writer.Write(word.Frequency);
		});

	public static RhymeWordList Read(BinaryReader reader)
		=> new(Read(reader, r => new Entry(reader.ReadString(), reader.ReadString(), reader.ReadSByte())));

	public IEnumerable<Result> EnumerateRhymes(string ipa, int maxSyllables)
	{
		if (maxSyllables <= 0)
			return [];

		var suffix = IpaHelper.GetRhymeSuffix(ipa, maxSyllables);
		if (string.IsNullOrEmpty(suffix))
			return [];

		var reverseSuffix = new string(Entry.FilterIpa(suffix, true).ToArray());
		return FeatureAdapters[0].FindAll(entry
			=> CompareStrings(entry.FilterIpa(reverse: true).Take(reverseSuffix.Length), reverseSuffix, false));
	}

	public class Builder : Builder<RhymeWordList>
	{
		private protected override RhymeWordList CreateInstance(InstanceData data) => new(data);
	}

	public readonly record struct Entry(string Word, string Ipa, sbyte Frequency) : IFeatureWord<Entry>
	{
		static int IFeatureWord.SortableFeatures => 1;
		static int IFeatureWord.AdditionalFeatures => 1;

		static IComparer<Entry> IFeatureWord<Entry>.GetFeatureComparer(int featureIndex) => IpaComparer.Instance;

		public static IEnumerable<char> FilterIpa(string ipa, bool reverse)
			=> (reverse ? ipa.ReverseEnumerator() : ipa).Where(c => !IPA_IGNORE.Contains(c));

		string IFeatureWord.Word => Word;

		public IEnumerable<char> FilterIpa(bool reverse) => FilterIpa(Ipa, reverse);

		private class IpaComparer : IComparer<Entry>
		{
			public static readonly IpaComparer Instance = new();

			public int Compare(Entry x, Entry y)
				=> CompareStrings(x.FilterIpa(true), y.FilterIpa(true), false);
		}
	}

	public readonly record struct Result
	{
		private readonly RhymeWordList owner;
		private readonly int index;

		public string Word => !Success ? string.Empty : owner.Entries[index].Word;
		public string Ipa => !Success ? string.Empty : owner.Entries[index].Ipa;
		public sbyte Frequency => !Success ? (sbyte)0 : owner.Entries[index].Frequency;

		[MemberNotNullWhen(true, nameof(owner))]
		public bool Success => owner is not null;

		public Result(RhymeWordList owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}

		public IEnumerable<Result> EnumerateRhymes(int maxSyllables)
			=> owner.EnumerateRhymes(Ipa, maxSyllables);
	}
}

public class ComparisonWordList : WordFeatureList<ComparisonWordList.Entry, ComparisonWordList.Result>
{
	public const char IPA_SEPARATOR = '§';

	protected ComparisonWordList(InstanceData data)
		: base(data)
	{ }

	protected override Result CreateResult(int index) => new(this, index);

	public void Write(BinaryWriter writer)
		=> Write(writer, (writer, word) =>
		{
			writer.Write(word.Word);
			writer.Write(word.Ipas);
		});

	public static ComparisonWordList Read(BinaryReader reader)
		=> new(Read(reader, r => new Entry(reader.ReadString(), reader.ReadString())));

	public class Builder : Builder<ComparisonWordList>
	{
		private protected override ComparisonWordList CreateInstance(InstanceData data) => new(data);
	}

	public readonly record struct Entry(string Word, string Ipas) : IFeatureWord<Entry>
	{
		static int IFeatureWord.SortableFeatures => 0;
		static int IFeatureWord.AdditionalFeatures => 1;

		static IComparer<Entry> IFeatureWord<Entry>.GetFeatureComparer(int featureIndex)
			=> throw new NotSupportedException();

		string IFeatureWord.Word => Word;
	}

	public readonly record struct Result
	{
		private readonly ComparisonWordList owner;
		private readonly int index;

		public string Word => owner.Entries[index].Word;
		public string[] Ipas => owner.Entries[index].Ipas.Split(IPA_SEPARATOR);

		[MemberNotNullWhen(true, nameof(owner))]
		public bool Success => owner is not null;

		public Result(ComparisonWordList owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}
	}
}

public class WordFormList : IReadOnlyList<WordFormList.Result>
{
	private const int FORMS_START_FLAG = int.MinValue;

	private readonly Entry[] entries;
	private readonly MapAdapter sortedAdapter;

	public int Count => entries.Length;

	public Result this[int index] => CreateResult(index);

	private WordFormList(Entry[] entries, int[] sortedMap)
	{
		this.entries = entries;
		this.sortedAdapter = new(this, sortedMap);
	}

	protected Result CreateResult(int index) => new(this, index);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<Result> GetEnumerator()
	{
		for (var i = 0; i < entries.Length; i++)
			yield return CreateResult(i);
	}

	public Result FindForm(string form)
	{
		var index = sortedAdapter.BinarySearchWeak((Entry entry) => string.Compare(entry.Form, form, StringComparison.InvariantCultureIgnoreCase));
		return index < 0 ? default : CreateResult(index);
	}

	public IEnumerable<Result> FindAllForms(string form)
	{
		var middle = sortedAdapter.BinarySearchWeak((Entry entry) => string.Compare(entry.Form, form, StringComparison.InvariantCultureIgnoreCase));
		if (middle < 0)
			yield break;

		//Gehe rückwärts
		for (var i = middle; i >= 0; i--)
		{
			var entry = sortedAdapter[i];
			if (!entry.Form.Equals(form, StringComparison.InvariantCultureIgnoreCase))
				break;
			yield return CreateResult(sortedAdapter.Map(i));
		}

		//Gehe vorwärts
		for (var i = middle + 1; i < Count; i++)
		{
			var entry = sortedAdapter[i];
			if (!entry.Form.Equals(form, StringComparison.InvariantCultureIgnoreCase))
				break;
			yield return CreateResult(sortedAdapter.Map(i));
		}
	}

	public void Write(BinaryWriter writer)
	{
		writer.Write7BitEncodedInt(entries.Length);
		foreach (var entry in entries)
		{
			writer.Write(entry.Form);
			writer.Write(entry.StemIndex);
		}

		foreach (var index in sortedAdapter.GetIndexes())
			writer.Write7BitEncodedInt(index);
	}

	public static WordFormList Read(BinaryReader reader)
	{
		var length = reader.Read7BitEncodedInt();
		var entries = new Entry[length];
		for (var i = 0; i < length; i++)
			entries[i] = new(reader.ReadString(), reader.ReadInt32());

		var sortedMap = new int[length];
		for (var i = 0; i < length; i++)
			sortedMap[i] = reader.Read7BitEncodedInt();

		return new WordFormList(entries, sortedMap);
	}

	public class Builder : IDisposable
	{
		private List<BuilderEntry>? entries = new();
		private List<BuilderEntry>? sortedEntries = new();

		private bool disposed;

		public bool TryAdd(string stem, IEnumerable<string> forms)
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(Builder));

			var useForms = forms.Where(f => !f.StartsWith('-') && !f.EndsWith('-') && !f.Equals(stem, StringComparison.InvariantCultureIgnoreCase)).ToArray();
			var startIndex = -1;
			if (useForms.Length != 0)
				startIndex = (entries!.Count + 1) | FORMS_START_FLAG;

			var stemEntry = new BuilderEntry(stem, entries!.Count, startIndex);
			entries.Add(stemEntry);
			sortedEntries!.Add(stemEntry);

			foreach (var form in useForms)
			{
				var formEntry = new BuilderEntry(form, entries.Count, stemEntry.Id);
				entries.Add(formEntry);
				sortedEntries.Add(formEntry);
			}

			return true;
		}

		public WordFormList Build()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(Builder));

			int i;
			sortedEntries!.Sort();

			i = 0;
			var resultEntries = new Entry[entries!.Count];
			foreach (var entry in entries)
			{
				resultEntries[i++] = new(entry.Form, entry.StemId);
			}

			i = 0;
			var sorted = new int[entries.Count];
			foreach (var entry in sortedEntries)
			{
				sorted[i++] = entry.Id;
			}

			return new WordFormList(resultEntries, sorted);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					entries?.Clear();
				}

				entries = null;

				disposed = true;
			}
		}

		public void Dispose()
		{
			// Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		private readonly record struct BuilderEntry(string Form, int Id, int StemId) : IComparable<BuilderEntry>
		{
			public int CompareTo(BuilderEntry other)
				=> string.Compare(Form, other.Form, StringComparison.InvariantCultureIgnoreCase);

			public bool Equals(BuilderEntry other)
				=> other.Id == Id;

			public override int GetHashCode()
				=> Id;
		}
	}

	private readonly record struct Entry(string Form, int StemIndex);

	public struct Result
	{
		private readonly WordFormList? owner;
		private readonly int index;

		internal int Index => index;

		public string Form => !Success ? string.Empty : owner.entries[index].Form;
		public bool IsStem => Success && (owner.entries[index].StemIndex & FORMS_START_FLAG) != 0;

		public Result? Stem
		{
			get
			{
				if (!Success)
					return null;

				var stemIndex = owner.entries[index].StemIndex;
				if ((stemIndex & FORMS_START_FLAG) != 0)
					return null;

				return owner.CreateResult(stemIndex);
			}
		}

		[MemberNotNullWhen(true, nameof(owner))]
		public bool Success => owner is not null;

		internal Result(WordFormList owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}

		public IEnumerable<Result> EnumerateForms()
		{
			if (!Success)
				yield break;

			var stemIndex = owner.entries[index].StemIndex;
			if ((stemIndex & FORMS_START_FLAG) != 0)
				yield break;

			for (var formIndex = stemIndex & ~FORMS_START_FLAG; formIndex < owner.entries.Length; formIndex++)
			{
				var entry = owner.entries[formIndex];
				if (entry.StemIndex != index)
					break;
				yield return owner.CreateResult(formIndex);
			}
		}

		public override bool Equals([NotNullWhen(true)] object? obj)
			=> obj is Result result
			&& owner == result.owner
			&& index == result.index;

		public override int GetHashCode()
			=> HashCode.Combine(owner, index);

		public static bool operator ==(Result left, Result right) => left.Equals(right);
		public static bool operator !=(Result left, Result right) => !left.Equals(right);
	}

	private class MapAdapter : IReadOnlyList<Entry>
	{
		private readonly WordFormList owner;
		private readonly int[] map;

		public int Count => map.Length;
		public Entry this[int index] => owner.entries[map[index]];

		public MapAdapter(WordFormList owner, int[] map)
		{
			this.owner = owner;
			this.map = map;
		}

		public IEnumerable<int> GetIndexes() => map;

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<Entry> GetEnumerator()
		{
			foreach (var i in map)
				yield return owner.entries[i];
		}

		public int Map(int index) => map[index];

		//public IEnumerable<Result> FindAll(Func<TEntry, int> comparer)
		//{
		//	var middle = this.BinarySearchWeak(comparer); // string.Compare(getValue(e), 0, prefix, 0, prefix.Length, comparison));
		//	if (middle < 0)
		//		yield break;

		//	//Gehe rückwärts
		//	for (var i = middle; i >= 0; i--)
		//	{
		//		var entry = this[i];
		//		if (comparer(entry) != 0)
		//			break;
		//		yield return owner.CreateResult(map[i]);
		//	}

		//	//Gehe vorwärts
		//	for (var i = middle + 1; i < Count; i++)
		//	{
		//		var entry = this[i];
		//		if (comparer(entry) != 0)
		//			break;
		//		yield return owner.CreateResult(map[i]);
		//	}
		//}
	}
}
