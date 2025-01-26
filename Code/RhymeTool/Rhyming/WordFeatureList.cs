using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Skinnix.RhymeTool.Rhyming;

public interface IFeatureWord
{
	static abstract int SortableFeatures { get; }
	static abstract int AdditionalFeatures { get; }

	string Word { get; }
}

public interface IFeatureWord<TSelf> : IFeatureWord
	where TSelf : IFeatureWord<TSelf>
{
	static abstract IComparer<TSelf> GetFeatureComparer(int featureIndex);
}

public abstract class WordFeatureList<TEntry, TResult> : IReadOnlyList<TResult>
	where TEntry : struct, IFeatureWord<TEntry>
{
	private static readonly IComparer<TEntry>[] featureComparers = Enumerable.Range(0, TEntry.SortableFeatures).Select(TEntry.GetFeatureComparer).ToArray();

	protected readonly TEntry[] Entries;
	protected readonly MapAdapter ReverseAdapter;
	protected readonly MapAdapter[] FeatureAdapters;

	public int Count => Entries.Length;
	public TResult this[int index] => CreateResult(index);

	private WordFeatureList(TEntry[] entries, int[] reverseMap, int[][] maps)
	{
		this.Entries = entries;
		this.ReverseAdapter = new(this, reverseMap);

		this.FeatureAdapters = new MapAdapter[maps.Length];
		for (var i = 0; i < maps.Length; i++)
			FeatureAdapters[i] = new(this, maps[i]);
	}

	protected WordFeatureList(InstanceData data)
		: this((InstanceDataImplementation)data)
	{ }

	private WordFeatureList(InstanceDataImplementation data)
		: this(data.Entries, data.ReverseMap, data.FeatureMaps)
	{ }

	protected abstract TResult CreateResult(int index);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<TResult> GetEnumerator()
	{
		for (var i = 0; i < Entries.Length; i++)
			yield return CreateResult(i);
	}

	[return: MaybeNull]
	public TResult FindWord(string word)
	{
		Func<TEntry, int> comparer = entry => WordComparer.IgnoreCase.Compare(entry, word);
		var index = Entries.BinarySearchWeak(comparer);
		return index < 0 ? default : CreateResult(index);
	}

	public IEnumerable<TResult> FindAllWords(string word)
	{
		Func<TEntry, int> comparer = entry => WordComparer.IgnoreCase.Compare(entry, word);
		var middle = Entries.BinarySearchWeak(comparer);
		if (middle < 0)
			yield break;

		//Gehe rückwärts
		for (var i = middle; i >= 0; i--)
		{
			var entry = Entries[i];
			if (comparer(entry) != 0)
				break;
			yield return CreateResult(i);
		}

		//Gehe vorwärts
		for (var i = middle + 1; i < Count; i++)
		{
			var entry = Entries[i];
			if (comparer(entry) != 0)
				break;
			yield return CreateResult(i);
		}
	}

	public IEnumerable<TResult> FindBySuffix(string suffix)
	{
		var reverse = suffix.Reverse();
		Func<TEntry, int> comparer = entry => ReverseWordComparer.IgnoreCase.Compare(entry, reverse);
		return ReverseAdapter.FindAll(comparer);
	}

	public void Write(BinaryWriter writer, Action<BinaryWriter, TEntry> writeEntry)
	{
		writer.Write7BitEncodedInt(Entries.Length);
		foreach (var entry in Entries)
			writeEntry(writer, entry);

		foreach (var index in ReverseAdapter.GetIndexes())
			writer.Write7BitEncodedInt(index);

		writer.Write7BitEncodedInt(FeatureAdapters.Length);
		foreach (var adapter in FeatureAdapters)
			foreach (var index in adapter.GetIndexes())
				writer.Write7BitEncodedInt(index);
	}

	public static InstanceData Read(BinaryReader reader, Func<BinaryReader, TEntry> readEntry)
	{
		var length = reader.Read7BitEncodedInt();
		var entries = new TEntry[length];
		for (var i = 0; i < length; i++)
			entries[i] = readEntry(reader);

		var reverseMap = new int[length];
		for (var i = 0; i < length; i++)
			reverseMap[i] = reader.Read7BitEncodedInt();

		var featureCount = reader.Read7BitEncodedInt();
		var featureMaps = new int[featureCount][];
		for (var i = 0; i < featureCount; i++)
		{
			var map = featureMaps[i] = new int[length];
			for (var j = 0; j < length; j++)
				map[j] = reader.Read7BitEncodedInt();
		}

		return new InstanceDataImplementation(entries, reverseMap, featureMaps);
	}

	protected static int CompareStrings(IEnumerable<char>? x, IEnumerable<char>? y, bool ignoreCase, CultureInfo? culture = null)
	{
		var xEnumerator = (x ?? []).GetEnumerator();
		var yEnumerator = (y ?? []).GetEnumerator();
		bool hasX, hasY;

		while ((hasX = xEnumerator.MoveNext()) & (hasY = yEnumerator.MoveNext()))
		{
			var charX = xEnumerator.Current;
			var charY = yEnumerator.Current;
			var comparison = culture is null
				? (!ignoreCase ? charX - charY
				: string.Compare(charX.ToString(), charY.ToString(), true))
				: culture.CompareInfo.Compare(
					new ReadOnlySpan<char>(ref charX), new ReadOnlySpan<char>(ref charY),
					ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);

			if (comparison != 0)
				return comparison;
		}

		if (hasX)
			return 1;
		else if (hasY)
			return -1;

		return 0;
	}

	protected static int CompareReverseStrings(ReadOnlySpan<char> x, ReadOnlySpan<char> y, bool ignoreCase, CultureInfo? culture = null)
	{
		var length = x.Length;
		if (y.Length < length)
			length = y.Length;

		for (var i = 1; i <= length; i++)
		{
			var charX = x[^i];
			var charY = y[^i];
			var comparison = culture is null ? charX - charY
				: culture.CompareInfo.Compare(
					x[^i..^(i - 1)], y[^i..^(i - 1)],
					ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);

			if (comparison != 0)
				return comparison;
		}

		return x.Length - y.Length;
	}

	public abstract class Builder<TList> : IDisposable
		where TList : WordFeatureList<TEntry, TResult>
	{
		private List<EntryWrapper>? entries = new();
		private List<EntryWrapper>? reverseEntries = new();
		private List<EntryWrapper>[]? featureEntries;

		private bool disposed;

		public Builder()
		{
			featureEntries = new List<EntryWrapper>[featureComparers.Length];
			for (var i = 0; i < featureEntries.Length; i++)
				featureEntries[i] = new();
		}

		public bool TryAdd(TEntry entry)
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(Builder<TList>));

			if (entries!.Count != 0 && entries[^1].Equals(entry))
				return false;

			var wrapper = new EntryWrapper(entry, entries.Count);
			entries.Add(wrapper);
			foreach (var featureEntryList in featureEntries!)
				featureEntryList.Add(wrapper);
			reverseEntries!.Add(wrapper);
			return true;
		}

		public TList Build()
		{
			if (disposed)
				throw new ObjectDisposedException(nameof(Builder<TList>));

			int i;
			entries!.Sort(new EntryWrapperComparer(WordComparer.IgnoreCase));
			reverseEntries!.Sort(new EntryWrapperComparer(ReverseWordComparer.IgnoreCase));
			for (i = 0; i < featureEntries!.Length; i++)
				featureEntries[i].Sort(new EntryWrapperComparer(featureComparers[i]));

			var resultEntries = new TEntry[entries.Count];
			var resultMap = new int[entries.Count];
			i = 0;
			foreach (var entry in entries)
			{
				resultMap[entry.Id] = i;
				resultEntries[i++] = entry.Entry;
			}

			var reverseMap = new int[entries.Count];
			i = 0;
			foreach (var reverseEntry in reverseEntries)
			{
				var index = resultMap[reverseEntry.Id];
				reverseMap[i++] = index;
			}

			var featureMaps = new int[featureEntries.Length][];
			for (var j = 0; j < featureEntries.Length; j++)
			{
				var map = featureMaps[j] = new int[entries.Count];
				i = 0;
				foreach (var entry in featureEntries[j])
				{
					var index = resultMap[entry.Id];
					map[i++] = index;
				}
			}

			var result = CreateInstance(new InstanceDataImplementation(resultEntries, reverseMap, featureMaps));
			return result;
		}

		private protected abstract TList CreateInstance(InstanceData data);

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					entries?.Clear();
					reverseEntries?.Clear();
					if (featureEntries is not null)
						foreach (var featureEntryList in featureEntries)
							featureEntryList?.Clear();
				}

				entries = null;
				reverseEntries = null;
				featureEntries = null;

				disposed = true;
			}
		}

		public void Dispose()
		{
			// Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}

	public abstract class InstanceData
	{
		private protected InstanceData() { }
	}

	private class InstanceDataImplementation : InstanceData
	{
		internal readonly TEntry[] Entries;
		internal readonly int[] ReverseMap;
		internal readonly int[][] FeatureMaps;

		public InstanceDataImplementation(TEntry[] entries, int[] reverseMap, int[][] featureMaps)
		{
			Entries = entries;
			ReverseMap = reverseMap;
			FeatureMaps = featureMaps;
		}
	}

	//public struct Result
	//{
	//	private readonly WordFeatureList<TEntry>? owner;
	//	private readonly int index;

	//	internal int Index => index;

	//	public string Word => IsEmpty ? string.Empty : owner.entries[index].Word;
	//	public TEntry Entry => IsEmpty ? default : owner.entries[index];

	//	[MemberNotNullWhen(false, nameof(owner))]
	//	public bool IsEmpty => owner is null;

	//	internal Result(WordFeatureList<TEntry> owner, int index)
	//	{
	//		this.owner = owner;
	//		this.index = index;
	//	}

	//	public override string ToString() => Word;

	//	public override bool Equals([NotNullWhen(true)] object? obj)
	//		=> obj is Result result
	//		&& owner == result.owner
	//		&& index == result.index;

	//	public override int GetHashCode()
	//		=> HashCode.Combine(owner, index);

	//	public static bool operator ==(Result left, Result right) => left.Equals(right);
	//	public static bool operator !=(Result left, Result right) => !left.Equals(right);
	//}

	//private readonly record struct Entry(string Word, TSortedData Data, TAdditionalData AdditionalData) : IComparable<Entry>
	//{
	//	public IEnumerable<char> FilterIpa(bool reverse) => FilterIpa(Ipa, reverse);

	//	public static IEnumerable<char> FilterIpa(string ipa, bool reverse)
	//		=> (reverse ? ipa.ReverseEnumerator() : ipa).Where(c => !IPA_IGNORE.Contains(c));

	//	public int CompareTo(Entry other)
	//		=> WordComparer.IgnoreCase.Compare(this, other);

	//	public class WordComparer(bool ignoreCase, CultureInfo? culture) : IComparer<string>, IComparer<Entry>, IComparer<EntryWrapper>
	//	{
	//		public static readonly WordComparer IgnoreCase = new(true, CultureInfo.InvariantCulture);

	//		public int Compare(string? x, string? y)
	//			=> CompareStrings(x, y, ignoreCase, culture);

	//		public int Compare(Entry x, Entry y)
	//			=> CompareStrings(x.Word, y.Word, ignoreCase, culture);

	//		public int Compare(EntryWrapper x, EntryWrapper y)
	//			=> x.Id == y.Id ? 0
	//			: CompareStrings(x.Entry.Word, y.Entry.Word, ignoreCase, culture);
	//	}

	//	public class ReverseWordComparer(bool ignoreCase, CultureInfo? culture) : IComparer<Entry>, IComparer<EntryWrapper>
	//	{
	//		public static readonly ReverseWordComparer IgnoreCase = new(true, CultureInfo.InvariantCulture);

	//		public int Compare(Entry x, Entry y)
	//			=> CompareStrings(x.Word.ReverseEnumerator(), y.Word.ReverseEnumerator(), ignoreCase, culture);

	//		public int Compare(EntryWrapper x, EntryWrapper y)
	//			=> x.Id == y.Id ? 0
	//			: CompareStrings(x.Entry.Word.ReverseEnumerator(), y.Entry.Word.ReverseEnumerator(), ignoreCase, culture);
	//	}

	//	public class IpaComparer : IComparer<Entry>, IComparer<EntryWrapper>
	//	{
	//		public static readonly IpaComparer Default = new();
			
	//		public int Compare(Entry x, Entry y)
	//			=> CompareStrings(x.FilterIpa(true), y.FilterIpa(true), false);

	//		public int Compare(EntryWrapper x, EntryWrapper y)
	//			=> x.Id == y.Id ? 0
	//			: CompareStrings(x.Entry.FilterIpa(true), y.Entry.FilterIpa(true), false);
	//	}
	//}

	private readonly record struct EntryWrapper(TEntry Entry, int Id)
	{
		public bool Equals(EntryWrapper other)
			=> other.Id == Id;

		public override int GetHashCode()
			=> Id;
	}

	private readonly struct EntryWrapperComparer(IComparer<TEntry> comparer) : IComparer<EntryWrapper>
	{
		public int Compare(EntryWrapper x, EntryWrapper y)
			=> comparer.Compare(x.Entry, y.Entry);
	}

	protected class WordComparer(bool ignoreCase, CultureInfo? culture) : IComparer<TEntry>, IComparer<EntryWrapper>
	{
		public static readonly WordComparer IgnoreCase = new(true, CultureInfo.InvariantCulture);

		public int Compare(TEntry x, string word)
			=> CompareStrings(x.Word, word, ignoreCase, culture);

		public int Compare(TEntry x, TEntry y)
			=> CompareStrings(x.Word, y.Word, ignoreCase, culture);

		int IComparer<EntryWrapper>.Compare(EntryWrapper x, EntryWrapper y)
			=> x.Id == y.Id ? 0
			: CompareStrings(x.Entry.Word, y.Entry.Word, ignoreCase, culture);
	}

	protected class ReverseWordComparer(bool ignoreCase, CultureInfo? culture) : IComparer<TEntry>, IComparer<EntryWrapper>
	{
		public static readonly ReverseWordComparer IgnoreCase = new(true, CultureInfo.InvariantCulture);

		public int Compare(TEntry x, string reversedWord)
			=> CompareStrings(x.Word.ReverseEnumerator(), reversedWord, ignoreCase, culture);

		public int Compare(TEntry x, TEntry y)
			=> CompareStrings(x.Word.ReverseEnumerator(), y.Word.ReverseEnumerator(), ignoreCase, culture);

		int IComparer<EntryWrapper>.Compare(EntryWrapper x, EntryWrapper y)
			=> x.Id == y.Id ? 0
			: CompareStrings(x.Entry.Word.ReverseEnumerator(), y.Entry.Word.ReverseEnumerator(), ignoreCase, culture);
	}

	protected class MapAdapter : IReadOnlyList<TEntry>
	{
		private readonly WordFeatureList<TEntry, TResult> owner;
		private readonly int[] map;

		public int Count => map.Length;
		public TEntry this[int index] => owner.Entries[map[index]];

		public MapAdapter(WordFeatureList<TEntry, TResult> owner, int[] map)
		{
			this.owner = owner;
			this.map = map;
		}

		public IEnumerable<int> GetIndexes() => map;

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<TEntry> GetEnumerator()
		{
			foreach (var i in map)
				yield return owner.Entries[i];
		}

		public IEnumerable<TResult> FindAll(Func<TEntry, int> comparer)
		{
			var middle = this.BinarySearchWeak(comparer); // string.Compare(getValue(e), 0, prefix, 0, prefix.Length, comparison));
			if (middle < 0)
				yield break;

			//Gehe rückwärts
			for (var i = middle; i >= 0; i--)
			{
				var entry = this[i];
				if (comparer(entry) != 0)
					break;
				yield return owner.CreateResult(map[i]);
			}

			//Gehe vorwärts
			for (var i = middle + 1; i < Count; i++)
			{
				var entry = this[i];
				if (comparer(entry) != 0)
					break;
				yield return owner.CreateResult(map[i]);
			}
		}
	}
}
