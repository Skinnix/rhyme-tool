using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Tar;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Skinnix.Dictionaries.Rhyming;

public class IpaRhymeList<TAdditionalData> : IReadOnlyList<IpaRhymeList<TAdditionalData>.Result>
{
	public const string IPA_IGNORE = "1ˈˌ§.̩̯̥̊̃̍̆͜͡";

	private readonly Entry[] entries;
	private readonly MapAdapter ipaAdapter;
	private readonly MapAdapter reverseAdapter;

	public int Count => entries.Length;
	public Result this[int index] => new(this, index);

	private IpaRhymeList(Entry[] entries, int[] ipaMap, int[] reverseMap)
	{
		this.entries = entries;

		ipaAdapter = new(this, ipaMap);
		reverseAdapter = new(this, reverseMap);
	}

	protected IpaRhymeList(InstanceData data)
		: this((InstanceDataImplementation)data)
	{ }

	private IpaRhymeList(InstanceDataImplementation data)
		: this(data.Entries, data.IpaMap, data.ReverseMap)
	{ }

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<Result> GetEnumerator()
	{
		for (var i = 0; i < entries.Length; i++)
			yield return new(this, i);
	}

	public Result FindWord(string word)
	{
		var index = Array.BinarySearch(entries, new(word, string.Empty, default!));
		return index < 0 ? default : new(this, index);
	}

	public IEnumerable<Result> FindAllWords(string word)
	{
		var searchEntry = new Entry(word, string.Empty, default!);
		var middle = Array.BinarySearch(entries, searchEntry);
		if (middle < 0)
			yield break;

		//Gehe rückwärts
		for (var i = middle; i >= 0; i--)
		{
			var entry = entries[i];
			if (Entry.WordComparer.IgnoreCase.Compare(entry, searchEntry) != 0)
				break;
			yield return new Result(this, i);
		}

		//Gehe vorwärts
		for (var i = middle + 1; i < Count; i++)
		{
			var entry = entries[i];
			if (Entry.WordComparer.IgnoreCase.Compare(entry, searchEntry) != 0)
				break;
			yield return new Result(this, i);
		}
	}

	public IEnumerable<Result> FindBySuffix(string suffix)
		=> reverseAdapter.FindAll(suffix.Reverse(), (s, entry)
			=> Entry.CompareStrings(entry.Word.ReverseEnumerator().Take(s.Length), s, true, CultureInfo.InvariantCulture));

	public void Write(BinaryWriter writer, Action<BinaryWriter, TAdditionalData> writeAdditionalData)
	{
		writer.Write7BitEncodedInt(entries.Length);
		foreach (var entry in entries)
		{
			writer.Write(entry.Word);
			writer.Write(entry.Ipa);
			writeAdditionalData(writer, entry.AdditionalData);
		}

		foreach (var index in ipaAdapter.GetIndexes())
			writer.Write7BitEncodedInt(index);

		foreach (var index in reverseAdapter.GetIndexes())
			writer.Write7BitEncodedInt(index);
	}

	public static InstanceData Read(BinaryReader reader, Func<BinaryReader, TAdditionalData> readAdditionalData)
	{
		var length = reader.Read7BitEncodedInt();
		var entries = new Entry[length];
		for (var i = 0; i < length; i++)
		{
			var word = reader.ReadString();
			var ipa = reader.ReadString();
			var additionalData = readAdditionalData(reader);

			entries[i] = new Entry(word, ipa, additionalData);
		}

		var ipaMap = new int[length];
		for (var i = 0; i < length; i++)
			ipaMap[i] = reader.Read7BitEncodedInt();

		var reverseMap = new int[length];
		for (var i = 0; i < length; i++)
			reverseMap[i] = reader.Read7BitEncodedInt();

		return new InstanceDataImplementation(entries, ipaMap, reverseMap);
	}

	public class Builder : IDisposable
	{
		private List<EntryWrapper>? entries = new(); //new(Entry.NonCollidingWordComparer.Instance);
		private List<EntryWrapper>? ipaEntries = new(); //new(Entry.NonCollidingIpaComparer.Instance);
		private List<EntryWrapper>? reverseEntries = new();

		private bool disposed;

		public bool TryAdd(string word, string ipa, TAdditionalData additionalData)
		{
			if (entries is null || ipaEntries is null || reverseEntries is null)
				throw new ObjectDisposedException(nameof(Builder));

			var entry = new Entry(word, ipa, additionalData);

			if (entries.Count != 0 && entries[^1].Equals(entry))
				return false;
			if (ipaEntries.Count != 0 && ipaEntries[^1].Equals(entry))
				return false;
			if (reverseEntries.Count != 0 && reverseEntries[^1].Equals(entry))
				return false;

			var wrapper = new EntryWrapper(entry, entries.Count);
			entries.Add(wrapper);
			ipaEntries.Add(wrapper);
			reverseEntries.Add(wrapper);
			return true;
		}

		public IpaRhymeList<TAdditionalData> Build()
		{
			if (entries is null || ipaEntries is null || reverseEntries is null)
				throw new ObjectDisposedException(nameof(Builder));

			entries.Sort(Entry.WordComparer.IgnoreCase);
			ipaEntries.Sort(Entry.IpaComparer.Default);
			reverseEntries.Sort(Entry.ReverseWordComparer.IgnoreCase);

			var resultEntries = new Entry[entries.Count];
			var resultMap = new int[entries.Count];
			var i = 0;
			foreach (var entry in entries)
			{
				resultMap[entry.Id] = i;
				resultEntries[i++] = entry.Entry;
			}

			var ipaMap = new int[entries.Count];
			i = 0;
			foreach (var ipaEntry in ipaEntries)
			{
				var index = resultMap[ipaEntry.Id];
				ipaMap[i++] = index;
			}

			var reverseMap = new int[entries.Count];
			i = 0;
			foreach (var reverseEntry in reverseEntries)
			{
				var index = resultMap[reverseEntry.Id];
				reverseMap[i++] = index;
			}

			return CreateInstance(new InstanceDataImplementation(resultEntries, ipaMap, reverseMap));
		}

		protected virtual IpaRhymeList<TAdditionalData> CreateInstance(InstanceData data)
			=> new(data);

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					entries?.Clear();
					ipaEntries?.Clear();
				}

				entries = null;
				ipaEntries = null;

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
		internal readonly Entry[] Entries;
		internal readonly int[] IpaMap;
		internal readonly int[] ReverseMap;

		public InstanceDataImplementation(Entry[] entries, int[] ipaMap, int[] reverseMap)
		{
			Entries = entries;
			IpaMap = ipaMap;
			ReverseMap = reverseMap;
		}
	}

	public struct Result
	{
		private readonly IpaRhymeList<TAdditionalData>? owner;
		private readonly int index;

		internal int Index => index;

		public string Word => IsEmpty ? string.Empty : owner.entries[index].Word;
		public string Ipa => IsEmpty ? string.Empty : owner.entries[index].Ipa;
		public TAdditionalData AdditionalData => IsEmpty ? default! : owner.entries[index].AdditionalData;

		[MemberNotNullWhen(false, nameof(owner))]
		public bool IsEmpty => owner is null;

		internal Result(IpaRhymeList<TAdditionalData> owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}

		public IEnumerable<char> FilterIpa(bool reverse) => IsEmpty ? [] : owner.entries[index].FilterIpa(reverse);

		public IEnumerable<Result> EnumerateGroup(int maxSyllables)
		{
			if (IsEmpty || maxSyllables <= 0)
				return [];

			var entry = owner.entries[index];
			var adapter = owner.ipaAdapter;

			var suffix = IpaHelper.GetRhymeSuffix(Ipa, maxSyllables);
			if (string.IsNullOrEmpty(suffix))
				return [];

			var reverseSuffix = new string(Entry.FilterIpa(suffix, true).ToArray());

			var word = Word;
			return adapter.FindAll(reverseSuffix, (s, entry)
				=> Entry.CompareStrings(entry.FilterIpa(reverse: true).Take(s.Length), s, false))
				.Where(r => r.Word != word);
		}

		public override string ToString() => Word;

		public override bool Equals([NotNullWhen(true)] object? obj)
			=> obj is Result result
			&& owner == result.owner
			&& index == result.index;

		public override int GetHashCode()
			=> HashCode.Combine(owner, index);

		public static bool operator ==(Result left, Result right) => left.Equals(right);
		public static bool operator !=(Result left, Result right) => !left.Equals(right);
	}

	private readonly record struct Entry(string Word, string Ipa, TAdditionalData AdditionalData) : IComparable<Entry>
	{
		public IEnumerable<char> FilterIpa(bool reverse) => FilterIpa(Ipa, reverse);

		public static IEnumerable<char> FilterIpa(string ipa, bool reverse)
			=> (reverse ? ipa.ReverseEnumerator() : ipa).Where(c => !IPA_IGNORE.Contains(c));

		public int CompareTo(Entry other)
			=> WordComparer.IgnoreCase.Compare(this, other);

		public static int CompareStrings(IEnumerable<char>? x, IEnumerable<char>? y, bool ignoreCase, CultureInfo? culture = null)
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

		public static int CompareReverseStrings(ReadOnlySpan<char> x, ReadOnlySpan<char> y, bool ignoreCase, CultureInfo? culture = null)
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

		public class WordComparer(bool ignoreCase, CultureInfo? culture) : IComparer<string>, IComparer<Entry>, IComparer<EntryWrapper>
		{
			public static readonly WordComparer IgnoreCase = new(true, CultureInfo.InvariantCulture);

			public int Compare(string? x, string? y)
				=> CompareStrings(x, y, ignoreCase, culture);

			public int Compare(Entry x, Entry y)
				=> CompareStrings(x.Word, y.Word, ignoreCase, culture);

			public int Compare(EntryWrapper x, EntryWrapper y)
				=> x.Id == y.Id ? 0
				: CompareStrings(x.Entry.Word, y.Entry.Word, ignoreCase, culture);
		}

		public class ReverseWordComparer(bool ignoreCase, CultureInfo? culture) : IComparer<Entry>, IComparer<EntryWrapper>
		{
			public static readonly ReverseWordComparer IgnoreCase = new(true, CultureInfo.InvariantCulture);

			public int Compare(Entry x, Entry y)
				=> CompareStrings(x.Word.ReverseEnumerator(), y.Word.ReverseEnumerator(), ignoreCase, culture);

			public int Compare(EntryWrapper x, EntryWrapper y)
				=> x.Id == y.Id ? 0
				: CompareStrings(x.Entry.Word.ReverseEnumerator(), y.Entry.Word.ReverseEnumerator(), ignoreCase, culture);
		}

		public class IpaComparer : IComparer<Entry>, IComparer<EntryWrapper>
		{
			public static readonly IpaComparer Default = new();
			
			public int Compare(Entry x, Entry y)
				=> CompareStrings(x.FilterIpa(true), y.FilterIpa(true), false);

			public int Compare(EntryWrapper x, EntryWrapper y)
				=> x.Id == y.Id ? 0
				: CompareStrings(x.Entry.FilterIpa(true), y.Entry.FilterIpa(true), false);
		}
	}

	private readonly record struct EntryWrapper(Entry Entry, int Id)
	{
		public bool Equals(EntryWrapper other)
			=> other.Id == Id;

		public override int GetHashCode()
			=> Id;
	}

	private class MapAdapter : IReadOnlyList<Entry>
	{
		private readonly IpaRhymeList<TAdditionalData> owner;
		private readonly int[] map;

		public int Count => map.Length;
		public Entry this[int index] => owner.entries[map[index]];

		public MapAdapter(IpaRhymeList<TAdditionalData> owner, int[] map)
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

		public IEnumerable<Result> FindAll(string term, Func<string, Entry, int> comparer)
		{
			var middle = this.BinarySearchWeak((Entry e) => comparer(term, e)); // string.Compare(getValue(e), 0, prefix, 0, prefix.Length, comparison));
			if (middle < 0)
				yield break;

			//Gehe rückwärts
			for (var i = middle; i >= 0; i--)
			{
				var entry = this[i];
				if (comparer(term, entry) != 0)
					break;
				yield return new(owner, map[i]);
			}

			//Gehe vorwärts
			for (var i = middle + 1; i < Count; i++)
			{
				var entry = this[i];
				if (comparer(term, entry) != 0)
					break;
				yield return new(owner, map[i]);
			}
		}
	}
}
