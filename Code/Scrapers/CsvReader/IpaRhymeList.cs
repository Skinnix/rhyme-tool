using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScraperBase;

namespace CsvReader;

public class IpaRhymeList : IReadOnlyList<IpaRhymeList.Result>
{
	private static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

	private readonly Entry[] entries;
	private readonly MapAdapter adapter;

	public int Count => entries.Length;
	public Result this[int index] => new(this, index);

	private IpaRhymeList(Entry[] entries, int[] ipaMap)
	{
		this.entries = entries;

		adapter = new(this, ipaMap);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<Result> GetEnumerator()
	{
		for (var i = 0; i < entries.Length; i++)
			yield return new(this, i);
	}

	public Result? Find(string word)
	{
		var index = Array.BinarySearch(entries, new(word, string.Empty));
		return index < 0 ? null : new(this, index);
	}

	public class Builder<TIpaDetail> : IDisposable
		where TIpaDetail : IWordIpa
	{
		private List<Entry>? entries = new(); //new(Entry.NonCollidingWordComparer.Instance);
		private List<Entry>? ipaEntries = new(); //new(Entry.NonCollidingIpaComparer.Instance);

		private bool disposed;

		public bool TryAdd<TWord>(TWord word)
			where TWord : IRhymableWord
		{
			if (entries is null || ipaEntries is null)
				throw new ObjectDisposedException(nameof(Builder<TIpaDetail>));

			var ipa = word.TryGetDetail<TIpaDetail>()?.Ipa;
			if (ipa is null)
				return false;

			var ipaReverse = ipa.ToCharArray();
			Array.Reverse(ipaReverse);
			var entry = new Entry(word.Word, new string(ipaReverse));

			if (entries.Count != 0 && entries[^1].Equals(entry))
				return false;
			if (ipaEntries.Count != 0 && ipaEntries[^1].Equals(entry))
				return false;

			entries.Add(entry);
			ipaEntries.Add(entry);
			return true;
		}

		public IpaRhymeList Build()
		{
			if (entries is null || ipaEntries is null)
				throw new ObjectDisposedException(nameof(Builder<TIpaDetail>));

			entries.Sort(Entry.NonCollidingWordComparer.Instance);
			ipaEntries.Sort(Entry.NonCollidingIpaComparer.Instance);

			var resultEntries = new Entry[entries.Count];
			entries.CopyTo(resultEntries, 0);

			var ipaMap = new int[entries.Count];
			var i = 0;
			foreach (var ipaEntry in ipaEntries)
			{
				var index = Array.BinarySearch(resultEntries, ipaEntry);
				ipaMap[i++] = index;
			}

			return new(resultEntries, ipaMap);
		}

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

	public struct Result
	{
		private readonly IpaRhymeList owner;
		private readonly int index;

		private string[]? rhymeSuffix;

		public string Word => owner.entries[index].Word;
		public string Ipa => owner.entries[index].Ipa.Reverse();

		public string[] RhymeSuffix
			=> rhymeSuffix ??= IpaHelper.GetRhymeSuffixArray(Ipa);

		internal Result(IpaRhymeList owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}

		public IEnumerable<Result> EnumerateGroup(int maxSyllables)
		{
			if (maxSyllables <= 0)
				return [];

			var entry = owner.entries[index];
			var adapter = owner.adapter;

			var prefix = string.Join(null, RhymeSuffix.TakeLast(maxSyllables)).Reverse();
			if (string.IsNullOrEmpty(prefix))
				return [];

			var word = Word;
			return adapter.FindAll(prefix).Where(r => r.Word != word);
		}

		public override string ToString() => Word;
	}

	private readonly record struct Entry(string Word, string Ipa) : IComparable<Entry>
	{
		public int CompareTo(Entry other)
			=> comparer.Compare(Word, other.Word);

		public class NonCollidingWordComparer : IComparer<Entry>
		{
			public static readonly NonCollidingWordComparer Instance = new();

			public int Compare(Entry x, Entry y)
			{
				var result = comparer.Compare(x.Word, y.Word);
				if (result == 0 && x.Ipa != y.Ipa)
					return 1;
				return result;
			}
		}

		public class NonCollidingIpaComparer : IComparer<Entry>
		{
			public static readonly NonCollidingIpaComparer Instance = new();

			public int Compare(Entry x, Entry y)
			{
				var result = comparer.Compare(x.Ipa, y.Ipa);
				if (result == 0 && x.Word != y.Word)
					return 1;
				return result;
			}
		}
	}

	private class MapAdapter : IReadOnlyList<Entry>
	{
		private readonly IpaRhymeList owner;
		private readonly int[] map;

		public int Count => map.Length;
		public Entry this[int index] => owner.entries[map[index]];

		public MapAdapter(IpaRhymeList owner, int[] map)
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

		public IEnumerable<Result> FindAll(string ipaPrefix)
		{
			var middle = this.BinarySearchWeak(e => string.Compare(e.Ipa, 0, ipaPrefix, 0, ipaPrefix.Length, StringComparison.Ordinal));
			if (middle < 0)
				yield break;

			//Gehe rückwärts
			for (var i = middle; i >= 0; i--)
			{
				var entry = this[i];
				if (!entry.Ipa.StartsWith(ipaPrefix))
					break;
				yield return new(owner, map[i]);
			}

			//Gehe vorwärts
			for (var i = middle + 1; i < Count; i++)
			{
				var entry = this[i];
				if (!entry.Ipa.StartsWith(ipaPrefix))
					break;
				yield return new(owner, map[i]);
			}
		}
	}
}
