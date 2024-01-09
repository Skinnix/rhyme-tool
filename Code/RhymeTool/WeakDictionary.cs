using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Skinnix.RhymeTool;

public class WeakDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	where TKey: notnull
	where TValue: class
{
	private readonly List<Entry> entries = new();

	public bool IsReadOnly => false;
	public int Count
	{
		get
		{
			Cleanup();
			return entries.Count;
		}
	}

	public ICollection<TKey> Keys => this.Select(e => e.Key).ToList();
	public ICollection<TValue> Values => this.Select(e => e.Value).ToList();

	public TValue this[TKey key]
	{
		get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
		set
		{
			var entry = entries.FirstOrDefault(e => e.Key.Equals(key));
			if (entry == null)
				entries.Add(new Entry(key, new WeakReference<TValue>(value)));
			else
				entry.Value.SetTarget(value);
		}
	}

	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
	{
		TValue? foundValue = null;
		Cleanup(item =>
		{
			if (item.Key.Equals(key))
				foundValue = item.Value;

			return false;
		});

		return (value = foundValue) != null;
	}

	public bool Contains(KeyValuePair<TKey, TValue> item) => TryGetValue(item.Key, out var value) && Equals(value, item.Value);
	public bool ContainsKey(TKey key) => TryGetValue(key, out _);

	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		Cleanup(item =>
		{
			array[arrayIndex++] = item;
			return false;
		});
	}

	public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);
	public void Add(TKey key, TValue value)
	{
		entries.Add(new Entry(key, new WeakReference<TValue>(value)));
	}

	public void Clear()
	{
		entries.Clear();
	}

	public bool Remove(TKey key)
	{
		var found = false;
		Cleanup(item =>
		{
			if (item.Key.Equals(key))
			{
				found = true;
				return true;
			}

			return false;
		});

		return found;
	}

	public bool Remove(KeyValuePair<TKey, TValue> item)
	{
		var found = false;
		Cleanup(foundItem =>
		{
			if (foundItem.Key.Equals(item.Key) && Equals(foundItem.Value, item.Value))
			{
				found = true;
				return true;
			}

			return false;
		});

		return found;
	}

	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
	{
		foreach (var entry in entries)
		{
			if (!entry.Value.TryGetTarget(out var value))
			{
				entries.Remove(entry);
				continue;
			}

			yield return new KeyValuePair<TKey, TValue>(entry.Key, value);
		}
	}

	public void Cleanup() => Cleanup(_ => false);
	private void Cleanup(Func<KeyValuePair<TKey, TValue>, bool> callback)
		=> entries.RemoveAll(e =>
		{
			if (!e.Value.TryGetTarget(out var value)) return true;

			return callback(new(e.Key, value));
		});

	private record Entry(TKey Key, WeakReference<TValue> Value)
	{
		public override int GetHashCode() => Key.GetHashCode();
	}
}
