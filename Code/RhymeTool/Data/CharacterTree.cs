using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data;

public class CharacterTree<TValue>
{
	private readonly ConcurrentDictionary<char, CharacterTree<TValue>> children = new();

	private bool hasValue;
	private TValue? value;
	private List<string> blacklist = new();

	public void Set(ReadOnlySpan<char> key, TValue value)
		=> Set(key, value, Enumerable.Empty<string>(), 0);
	public void Set(string key, TValue value, IEnumerable<string> blacklistedKeys)
		=> Set(key, value, blacklistedKeys.Where(b => b.StartsWith(key)), 0);
	private void Set(ReadOnlySpan<char> key, TValue value, IEnumerable<string> blacklistedKeys, int depth)
	{
		if (key.Length == 0)
		{
			this.value = value;
			hasValue = true;

			foreach (var blacklistedKey in blacklistedKeys)
				if (blacklistedKey.Length > depth)
					blacklist.Add(blacklistedKey[depth..]);

			return;
		}

		var c = key[0];
		if (!children.TryGetValue(c, out var child))
		{
			child = new();
			children.TryAdd(c, child);
		}

		child.Set(key[1..], value, blacklistedKeys, depth + 1);
	}

	public TValue Get(ReadOnlySpan<char> key)
	{
		if (key.Length == 0)
		{
			if (hasValue)
				return value!;
			else
				throw new KeyNotFoundException();
		}

		var c = key[0];
		if (!children.TryGetValue(c, out var child))
			throw new KeyNotFoundException();

		return child.Get(key[1..]);
	}

	public bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out TValue value, bool ignoreSuffix = false)
	{
		if (key.Length == 0)
		{
			value = this.value;
			return hasValue;
		}

		if (ignoreSuffix && hasValue)
		{
			value = this.value!;
			return true;
		}

		if (!children.TryGetValue(key[0], out var child))
		{
			value = default;
			return false;
		}

		return child.TryGetValue(key[1..], out value);
	}

	public int TryRead(ReadOnlySpan<char> key, [MaybeNull] out TValue value, bool ignoreEmpty = true)
	{
		if (hasValue && !ignoreEmpty)
		{
			//Prüfe Blacklist
			foreach (var blacklisted in blacklist)
			{
				if (key.StartsWith(blacklisted))
				{
					value = default;
					return -1;
				}
			}

			value = this.value!;
			return 0;
		}
		else if (key.Length == 0)
		{
			value = default;
			return -1;
		}

		if (!children.TryGetValue(key[0], out var child))
		{
			value = default;
			return -1;
		}

		var length = child.TryRead(key[1..], out value, ignoreEmpty: false);
		if (length == -1)
			return -1;

		return length + 1;
	}
}
