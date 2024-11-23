using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.IO;

public class IniFile : IReadOnlyDictionary<string, string>
{
	public Section DefaultSection { get; }
	public Section.Collection Sections { get; }
	public StringComparison DefaultComparison { get; }

	public IEnumerable<Section> AllSections => Sections.Values.Append(DefaultSection);

	public int Count => AllSections.Select(s => s.Count).Sum();
	public string this[string key] => AllSections
		.Select(s => (Found: s.TryGetValue(key, out var v), Value: v))
		.First(v => v.Found)
		.Value!;

	public IEnumerable<string> Keys => AllSections.SelectMany(s => s.Keys);
	public IEnumerable<string> Values => AllSections.SelectMany(s => s.Values);

	public IniFile(Section defaultSection, IEnumerable<KeyValuePair<string, Section>>? values, StringComparison defaultComparison = default)
	{
		DefaultSection = defaultSection;
		Sections = new(values ?? [], defaultComparison);
		DefaultComparison = defaultComparison;
	}

	public bool ContainsKey(string key) => ContainsKey(key, DefaultComparison);
	public bool ContainsKey(string key, StringComparison comparison) => AllSections.Any(s => s.ContainsKey(key, comparison));

	public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => TryGetValue(key, out value, DefaultComparison);
	public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value, StringComparison comparison)
	{
		foreach (var section in AllSections)
			if (section.TryGetValue(key, out value, comparison))
				return true;

		value = null;
		return false;
	}

	public bool TryGetValue(string section, string key, [MaybeNullWhen(false)] out string value) => TryGetValue(section, key, out value, DefaultComparison);
	public bool TryGetValue(string section, string key, [MaybeNullWhen(false)] out string value, StringComparison comparison)
	{
		if (Sections.TryGetValue(section, out var s, comparison))
			return s.TryGetValue(key, out value, comparison);

		value = null;
		return false;
	}

	public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => AllSections.SelectMany(s => s).GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	#region Read
	public static IniFile Read(StreamReader reader, StringComparison defaultComparison = default)
	{
		List<KeyValuePair<string, List<KeyValuePair<string, string>>>> sections = new();
		List<KeyValuePair<string, string>> defaultSection = [];
		List<KeyValuePair<string, string>> currentSection = defaultSection;

		string? line;
		while ((line = reader.ReadLine()) is not null)
			ReadLine(sections, ref currentSection, line.Trim());

		return new IniFile(new Section(defaultSection), sections.Select(s => KeyValuePair.Create(s.Key, new Section(s.Value))), defaultComparison);
	}

	public static async Task<IniFile> ReadAsync(StreamReader reader, StringComparison defaultComparison = default)
	{
		List<KeyValuePair<string, List<KeyValuePair<string, string>>>> sections = new();
		List<KeyValuePair<string, string>> defaultSection = [];
		List<KeyValuePair<string, string>> currentSection = defaultSection;

		string? line;
		while ((line = await reader.ReadLineAsync()) is not null)
			ReadLine(sections, ref currentSection, line.Trim());

		return new IniFile(new Section(defaultSection), sections.Select(s => KeyValuePair.Create(s.Key, new Section(s.Value))), defaultComparison);
	}

	private static void ReadLine(List<KeyValuePair<string, List<KeyValuePair<string, string>>>> sections,
		ref List<KeyValuePair<string, string>> currentSection,
		string line)
	{
		if (string.IsNullOrWhiteSpace(line))
			return;

		if (line.StartsWith('[') && line.EndsWith(']'))
		{
			var sectionName = line[1..^1];
			currentSection = [];
			sections.Add(new(sectionName, currentSection));
			return;
		}

		var split = line.Split('=', 2, StringSplitOptions.TrimEntries);
		if (split.Length == 2 && !string.IsNullOrWhiteSpace(split[0]) && !string.IsNullOrWhiteSpace(split[1]))
		{
			currentSection!.Add(new(split[0], split[1]));
			return;
		}

		currentSection!.Add(new(line, ""));
	}
	#endregion

	private class StringDictionary<TValue> : IReadOnlyDictionary<string, TValue>
	{
		private readonly KeyValuePair<string, TValue>[] values;

		public int Count => values.Length;
		public TValue this[string key] => values.First(v => v.Key == key).Value;

		public IEnumerable<string> Keys => values.Select(v => v.Key);
		public IEnumerable<TValue> Values => values.Select(v => v.Value);

		public StringDictionary(IEnumerable<KeyValuePair<string, TValue>> values)
		{
			this.values = values.ToArray();
		}

		bool IReadOnlyDictionary<string, TValue>.ContainsKey(string key) => ContainsKey(key, default);
		public bool ContainsKey(string key, StringComparison comparison) => values.Any(s => s.Key.Equals(key, comparison));

		bool IReadOnlyDictionary<string, TValue>.TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
			=> TryGetValue(key, out value, default);
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value, StringComparison comparison)
		{
			foreach (var v in values)
			{
				if (v.Key.Equals(key, comparison))
				{
					value = v.Value;
					return true;
				}
			}

			value = default;
			return false;
		}

		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
		{
			foreach (var v in values)
				yield return v;
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public class Section : IReadOnlyDictionary<string, string>
	{
		private readonly StringDictionary<string> values;

		public StringComparison DefaultComparison { get; init; } = default;

		public int Count => values.Count;
		public string this[string key] => values[key];
		public IEnumerable<string> Keys => values.Keys;
		public IEnumerable<string> Values => values.Values;

		public Section(IEnumerable<KeyValuePair<string, string>> values)
		{
			this.values = new(values);
		}

		public bool ContainsKey(string key) => ContainsKey(key, DefaultComparison);
		public bool ContainsKey(string key, StringComparison comparison = default) => values.ContainsKey(key, comparison);

		public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => TryGetValue(key, out value, DefaultComparison);
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value, StringComparison comparison) => values.TryGetValue(key, out value, comparison);

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public class Collection : IReadOnlyDictionary<string, Section>
		{
			private readonly StringDictionary<Section> values;

			public StringComparison DefaultComparison { get; } = default;

			public int Count => values.Count;
			public Section this[string key] => values[key];

			public IEnumerable<string> Keys => values.Keys;
			public IEnumerable<Section> Values => values.Values;

			public Collection(IEnumerable<KeyValuePair<string, Section>> values, StringComparison defaultComparison = default)
			{
				this.values = new(values);
				DefaultComparison = defaultComparison;
			}

			public bool ContainsKey(string key) => ContainsKey(key, DefaultComparison);
			public bool ContainsKey(string key, StringComparison comparison) => values.ContainsKey(key, comparison);

			public bool TryGetValue(string key, [MaybeNullWhen(false)] out Section value) => TryGetValue(key, out value, DefaultComparison);
			public bool TryGetValue(string key, [MaybeNullWhen(false)] out Section value, StringComparison comparison) => values.TryGetValue(key, out value, comparison);

			public IEnumerator<KeyValuePair<string, Section>> GetEnumerator() => values.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
