// See https://aka.ms/new-console-template for more information
using System.Text;
using CsvReader;

Console.WriteLine("Hello, World!");

//var input = @"C:\Users\Hendrik\Downloads\de.csv";
var input = @"C:\Users\Hendrik\Downloads\DAWB_words1.txt";
var frequencies = @"C:\Users\Hendrik\Downloads\dwds_lemmata_2025-01-16.csv";
var output = @"C:\Users\Hendrik\Downloads\DAWB_words2.txt";

IpaRhymeList words;
var wordFrequencies = new Dictionary<string, int>();

using (var wordsBuilder = new IpaRhymeList.Builder<IWordIpa>())
using (var reader = new StreamReader(input))
{
	string? line;
	while ((line = reader.ReadLine()) != null)
	{
		var split = line.Split(';', 2);
		if (split.Length < 2)
			continue;

		var key = split[0].Trim();
		var value = split[1].Trim('/');

		if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
			continue;

		wordsBuilder.TryAdd(new IpaWord(key, value));
	}

	words = wordsBuilder.Build();
}

using (var reader = new StreamReader(frequencies))
{
	reader.ReadLine();
	string? line;
	while ((line = reader.ReadLine()) != null)
	{
		var split = line.Split("\",\"");
		if (split.Length < 6)
			continue;

		var word = split[0].TrimStart('"');
		if (!int.TryParse(split[5].TrimEnd('"'), out var frequency))
			continue;

		wordFrequencies[word] = frequency;
	}
}

using (var writer = new StreamWriter(output, false, Encoding.UTF8))
{
	foreach (var word in words)
	{
		if (!wordFrequencies.TryGetValue(word.Word, out var frequency))
			frequency = -1;

		writer.WriteLine($"{word.Word};{word.Ipa};{frequency}");
	}
}

GC.Collect();

Console.WriteLine($"{words.Count} words loaded");

var search = Console.ReadLine()?.Trim();

while (!string.IsNullOrWhiteSpace(search))
{
	var word = words.Find(search);
	if (word == null)
	{
		Console.WriteLine("Wort nicht gefunden!");
	}
	else
	{
		Console.WriteLine($"Wort gefunden: {word.Value.Word} ({word.Value.Ipa})");

		var foundRhymes = new HashSet<IpaRhymeList.Result>();
		var maxSyllables = word.Value.RhymeSuffix.Length;
		for (var i = maxSyllables; i > 0; i--)
		{
			var rhymes = word.Value.EnumerateGroup(i).ToArray();
			foreach (var rhyme in rhymes)
			{
				if (foundRhymes.Add(rhyme))
				{
					if (rhyme.Word.EndsWith(word.Value.Word, StringComparison.OrdinalIgnoreCase))
					{
						Console.WriteLine($"Zusammensetzung: {rhyme.Word}");
					}
					else
					{
						Console.WriteLine($"Reim[{i}]: {rhyme.Word}");
					}
				}
			}
		}
	}

	Console.WriteLine();
	Console.WriteLine();
	search = Console.ReadLine()?.Trim();
}