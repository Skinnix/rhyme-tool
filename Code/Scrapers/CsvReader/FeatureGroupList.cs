using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScraperBase;

namespace CsvReader;

public class FeatureGroupList : IReadOnlyList<FeatureGroupList.Result>
{
	private static readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

	private readonly Entry[] entries;
	private readonly MapAdapter[] adapters;

	public int Count => entries.Length;
	public Result this[int index] => new(this, index);

	private FeatureGroupList(Entry[] entries, int[][] featureMaps)
	{
		this.entries = entries;

		adapters = featureMaps.Select((m, featureIndex) => new MapAdapter(this, m, featureIndex)).ToArray();
	}

	private MapAdapter GetAdapter(int featureIndex)
		=> adapters[featureIndex];

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<Result> GetEnumerator()
	{
		for (var i = 0; i < entries.Length; i++)
			yield return new(this, i);
	}

	public Result? Find(string word)
	{
		var index = Array.BinarySearch(entries, new(word, Enumerable.Range(0, adapters.Length).Select(_ => -1).ToArray()));
		return index < 0 ? null : new(this, index);
	}

	public class Builder(params WordFeature[] features) : IDisposable
	{
		private SortedSet<Entry>? entries = new();
		private Dictionary<string, FeatureGroup>[]? featureGroups = Enumerable.Range(0, features.Length).Select(_ => new Dictionary<string, FeatureGroup>()).ToArray();

		private bool disposed;

		public bool TryAdd<TWord>(TWord word)
			where TWord : IRhymableWord
		{
			if (entries == null || featureGroups == null)
				throw new ObjectDisposedException(nameof(Builder));

			if (word.Word.EndsWith("opf"))
			{ }

			var featureValues = new string[features.Length];
			var groupIndexes = new int[features.Length];
			for (var featureIndex = 0; featureIndex < features.Length; featureIndex++)
			{
				var feature = features[featureIndex];
				var featureValue = feature.GetFeatureValue(word);
				if (featureValue == null)
					return false;
				featureValues[featureIndex] = featureValue;

				var groups = featureGroups[featureIndex];
				if (!groups.TryGetValue(featureValue, out var group))
					groups.Add(featureValue, group = new(groups.Count));
				else
					group.Count++;

				groupIndexes[featureIndex] = group.Index;
			}

			entries.Add(new(word.Word, groupIndexes));
			return true;
		}

		public FeatureGroupList Build()
		{
			if (entries == null || featureGroups == null)
				throw new ObjectDisposedException(nameof(Builder));

			var featureLists = new List<int>[featureGroups.Length][];
			foreach ((var featureIndex, var groups) in featureGroups.Index())
			{
				var lists = featureLists[featureIndex] = new List<int>[groups.Count];
				foreach (var group in groups.Values)
					lists[group.Index] = new(group.Count);
			}

			var resultEntries = new FeatureGroupList.Entry[entries.Count];
			foreach ((var index, var entry) in entries.Index())
			{
				resultEntries[index] = new(entry.Word, entry.FeatureGroups);
				foreach ((var featureIndex, var groups) in featureGroups.Index())
				{
					var lists = featureLists[featureIndex];
					var group = lists[entry.FeatureGroups[featureIndex]];
					group.Add(index);
				}
			}

			var featureMaps = new int[featureGroups.Length][];
			foreach ((var featureIndex, var lists) in featureLists.Index())
			{
				var featureMap = featureMaps[featureIndex] = new int[entries.Count];
				var i = 0;
				foreach (var group in lists)
				{
					group.CopyTo(featureMap, i);
					i += group.Count;
				}
			}

			return new(resultEntries, featureMaps);
		}

		private readonly record struct Entry(string Word, int[] FeatureGroups) : IComparable<Entry>
		{
			public int CompareTo(Entry other)
				=> comparer.Compare(Word, other.Word);
		}

		private class FeatureGroup
		{
			public readonly int Index;
			public int Count;

			public FeatureGroup(int index, int count = 1)
			{
				Index = index;
				Count = count;
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					entries?.Clear();
				}

				if (featureGroups is not null)
				{
					foreach (var group in featureGroups)
					{
						group.Clear();
					}
				}

				entries = null;
				featureGroups = null;

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

	public readonly struct Result
	{
		private readonly FeatureGroupList owner;
		private readonly int index;

		public string Word => owner.entries[index].Word;

		internal Result(FeatureGroupList owner, int index)
		{
			this.owner = owner;
			this.index = index;
		}

		public IEnumerable<Result> EnumerateGroup(int featureIndex)
		{
			var entry = owner.entries[index];
			var featureGroup = entry.FeatureGroups[featureIndex];
			if (featureGroup == -1)
				return [];

			var adapter = owner.GetAdapter(featureIndex);
			return adapter.FindAll(featureGroup);
		}

		public override string ToString() => Word;
	}

	private readonly record struct Entry(string Word, int[] FeatureGroups) : IComparable<Entry>
	{
		public int CompareTo(Entry other)
			=> comparer.Compare(Word, other.Word);
	}

	private class MapAdapter : IReadOnlyList<Entry>
	{
		private readonly FeatureGroupList owner;
		private readonly int[] map;
		private readonly int featureIndex;

		public int Count => map.Length;
		public Entry this[int index] => owner.entries[map[index]];

		public MapAdapter(FeatureGroupList owner, int[] map, int featureIndex)
		{
			this.owner = owner;
			this.map = map;
			this.featureIndex = featureIndex;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<Entry> GetEnumerator()
		{
			foreach (var i in map)
				yield return owner.entries[i];
		}

		public IEnumerable<Result> FindAll(int featureGroup)
		{
			var middle = this.BinarySearch(e => e.FeatureGroups[featureIndex], featureGroup);
			if (middle < 0)
				yield break;

			//Gehe rückwärts
			for (var i = middle; i >= 0; i--)
			{
				var entry = this[i];
				if (entry.FeatureGroups[featureIndex] != featureGroup)
					break;
				yield return new(owner, map[i]);
			}

			//Gehe vorwärts
			for (var i = middle + 1; i < Count; i++)
			{
				var entry = this[i];
				if (entry.FeatureGroups[featureIndex] != featureGroup)
					break;
				yield return new(owner, map[i]);
			}
		}
	}
}
