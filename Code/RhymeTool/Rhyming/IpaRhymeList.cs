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

namespace Skinnix.RhymeTool.Rhyming;

public class IpaRhymeList<TAdditionalData> : IReadOnlyList<IpaRhymeList<TAdditionalData>.Result>
{
	private readonly Entry[] entries;
	private readonly PrefixMapAdapter ipaAdapter;
	private readonly PrefixMapAdapter reverseAdapter;

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
			if (entry.CompareTo(searchEntry) != 0)
				break;
			yield return new Result(this, i);
		}

		//Gehe vorwärts
		for (var i = middle + 1; i < Count; i++)
		{
			var entry = entries[i];
			if (entry.CompareTo(searchEntry) != 0)
				break;
			yield return new Result(this, i);
		}
	}

	public IEnumerable<Result> FindBySuffix(string suffix)
		=> reverseAdapter.FindAll(suffix, (suffix, entry)
			=> Entry.CompareReverseStrings(entry.Word.AsSpan()[^(suffix.Length <= entry.Word.Length ? suffix.Length : entry.Word.Length)..], suffix, true, CultureInfo.InvariantCulture));

	public class Builder<TIpaDetail> : IDisposable
		where TIpaDetail : IWordIpa
	{
		private List<Entry>? entries = new(); //new(Entry.NonCollidingWordComparer.Instance);
		private List<Entry>? ipaEntries = new(); //new(Entry.NonCollidingIpaComparer.Instance);
		private List<Entry>? reverseEntries = new();

		private bool disposed;

		public bool TryAdd<TWord>(TWord word, TAdditionalData additionalData)
			where TWord : IRhymableWord
		{
			if (entries is null || ipaEntries is null || reverseEntries is null)
				throw new ObjectDisposedException(nameof(Builder<TIpaDetail>));

			var ipa = word.TryGetDetail<TIpaDetail>()?.Ipa;
			if (ipa is null)
				return false;

			var ipaReverse = ipa.ToCharArray();
			Array.Reverse(ipaReverse);
			var entry = new Entry(word.Word, new string(ipaReverse), additionalData);

			if (entries.Count != 0 && entries[^1].Equals(entry))
				return false;
			if (ipaEntries.Count != 0 && ipaEntries[^1].Equals(entry))
				return false;
			if (reverseEntries.Count != 0 && reverseEntries[^1].Equals(entry))
				return false;

			entries.Add(entry);
			ipaEntries.Add(entry);
			reverseEntries.Add(entry);
			return true;
		}

		public IpaRhymeList<TAdditionalData> Build()
		{
			if (entries is null || ipaEntries is null || reverseEntries is null)
				throw new ObjectDisposedException(nameof(Builder<TIpaDetail>));

			entries.Sort(Entry.WordComparer.NonCollidingIgnoreCase);
			ipaEntries.Sort(Entry.IpaComparer.NonCollidingIgnoreCase);
			reverseEntries.Sort(Entry.ReverseWordComparer.NonCollidingIgnoreCase);

			var resultEntries = new Entry[entries.Count];
			entries.CopyTo(resultEntries, 0);

			var ipaMap = new int[entries.Count];
			var i = 0;
			foreach (var ipaEntry in ipaEntries)
			{
				var index = Array.BinarySearch(resultEntries, ipaEntry);
				ipaMap[i++] = index;
			}

			var reverseMap = new int[entries.Count];
			i = 0;
			foreach (var reverseEntry in reverseEntries)
			{
				var index = Array.BinarySearch(resultEntries, reverseEntry);
				reverseMap[i++] = index;
			}

			return CreateInstance(new InstanceDataImplementation(resultEntries, ipaMap, reverseMap));
		}

		private protected virtual IpaRhymeList<TAdditionalData> CreateInstance(InstanceData data)
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

		private string[]? ipaSyllables;

		internal int Index => index;

		public string Word => IsEmpty ? string.Empty : owner.entries[index].Word;
		public string Ipa => IsEmpty ? string.Empty : owner.entries[index].Ipa.Reverse();
		public TAdditionalData AdditionalData => IsEmpty ? default! : owner.entries[index].AdditionalData;

		public string[] IpaSyllables
			=> IsEmpty ? [] : ipaSyllables ??= IpaHelper.SplitSyllables(Ipa).ToArray();

		[MemberNotNullWhen(false, nameof(owner))]
		public bool IsEmpty => owner is null;

		internal Result(IpaRhymeList<TAdditionalData> owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}

		public IEnumerable<Result> EnumerateGroup(int maxSyllables)
		{
			if (IsEmpty || maxSyllables <= 0)
				return [];

			var entry = owner.entries[index];
			var adapter = owner.ipaAdapter;

			var prefix = string.Join(null, IpaSyllables.TakeLast(maxSyllables)).Reverse();
			if (string.IsNullOrEmpty(prefix))
				return [];

			var word = Word;
			return adapter.FindAll(prefix, (ipaPrefix, entry)
				=> Entry.CompareStrings(entry.Ipa.AsSpan()[0..(prefix.Length <= entry.Ipa.Length ? prefix.Length : entry.Ipa.Length)], prefix, false, null))
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
		public int CompareTo(Entry other)
			=> WordComparer.IgnoreCase.Compare(this, other);

		public static int CompareStrings(ReadOnlySpan<char> x, ReadOnlySpan<char> y, bool ignoreCase, CultureInfo? culture = null)
		{
			var length = x.Length;
			if (y.Length < length)
				length = y.Length;

			for (var i = 0; i < length; i++)
			{
				var charX = x[i];
				var charY = y[i];
				var comparison = culture is null ? (!ignoreCase ? charX - charY : string.Compare(charX.ToString(), charY.ToString(), true))
					: culture.CompareInfo.Compare(
						x[i..(i + 1)], y[i..(i + 1)],
						ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);

				if (comparison != 0)
					return comparison;
			}

			return x.Length - y.Length;
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

		public class WordComparer(bool avoidCollisions, bool ignoreCase, CultureInfo? culture) : IComparer<Entry>
		{
			public static readonly WordComparer Strict = new(false, false, CultureInfo.InvariantCulture);
			public static readonly WordComparer NonCollidingStrict = new(true, false, CultureInfo.InvariantCulture);
			public static readonly WordComparer IgnoreCase = new(false, true, CultureInfo.InvariantCulture);
			public static readonly WordComparer NonCollidingIgnoreCase = new(true, true, CultureInfo.InvariantCulture);

			public int Compare(Entry x, Entry y)
			{
				var result = CompareStrings(x.Word, y.Word, ignoreCase, culture);
				if (avoidCollisions && result == 0 && x.Ipa != y.Ipa)
					return new IpaComparer(avoidCollisions, ignoreCase, culture).Compare(x, y);

				return result;
			}
		}

		public class ReverseWordComparer(bool avoidCollisions, bool ignoreCase, CultureInfo? culture) : IComparer<Entry>
		{
			public static readonly ReverseWordComparer Strict = new(false, false, CultureInfo.InvariantCulture);
			public static readonly ReverseWordComparer NonCollidingStrict = new(true, false, CultureInfo.InvariantCulture);
			public static readonly ReverseWordComparer IgnoreCase = new(false, true, CultureInfo.InvariantCulture);
			public static readonly ReverseWordComparer NonCollidingIgnoreCase = new(true, true, CultureInfo.InvariantCulture);

			public int Compare(Entry x, Entry y)
			{
				var result = CompareReverseStrings(x.Word, y.Word, ignoreCase, culture);
				if (avoidCollisions && result == 0 && x.Ipa != y.Ipa)
					return new IpaComparer(avoidCollisions, ignoreCase, culture).Compare(x, y);

				return result;
			}
		}

		public class IpaComparer(bool avoidCollisions, bool ignoreCase, CultureInfo? culture) : IComparer<Entry>
		{
			public static readonly IpaComparer Strict = new(false, false, CultureInfo.InvariantCulture);
			public static readonly IpaComparer NonCollidingStrict = new(true, false, CultureInfo.InvariantCulture);
			public static readonly IpaComparer IgnoreCase = new(false, true, CultureInfo.InvariantCulture);
			public static readonly IpaComparer NonCollidingIgnoreCase = new(true, true, CultureInfo.InvariantCulture);

			public int Compare(Entry x, Entry y)
			{
				var result = CompareStrings(x.Ipa, y.Ipa, false);
				if (avoidCollisions && result == 0 && x.Word != y.Word)
					return new WordComparer(avoidCollisions, ignoreCase, culture).Compare(x, y);

				return result;
			}
		}
	}

	private class PrefixMapAdapter : IReadOnlyList<Entry>
	{
		private readonly IpaRhymeList<TAdditionalData> owner;
		private readonly int[] map;

		public int Count => map.Length;
		public Entry this[int index] => owner.entries[map[index]];

		public PrefixMapAdapter(IpaRhymeList<TAdditionalData> owner, int[] map)
		{
			this.owner = owner;
			this.map = map;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<Entry> GetEnumerator()
		{
			foreach (var i in map)
				yield return owner.entries[i];
		}

		public IEnumerable<Result> FindAll(string prefix, Func<string, Entry, int> comparer)
		{
			var middle = this.BinarySearchWeak(e => comparer(prefix, e)); // string.Compare(getValue(e), 0, prefix, 0, prefix.Length, comparison));
			if (middle < 0)
				yield break;

			//Gehe rückwärts
			for (var i = middle; i >= 0; i--)
			{
				var entry = this[i];
				if (comparer(prefix, entry) != 0)
					break;
				yield return new(owner, map[i]);
			}

			//Gehe vorwärts
			for (var i = middle + 1; i < Count; i++)
			{
				var entry = this[i];
				if (comparer(prefix, entry) != 0)
					break;
				yield return new(owner, map[i]);
			}
		}
	}
}
