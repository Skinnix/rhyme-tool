// See https://aka.ms/new-console-template for more information
using CsvReader;

Console.WriteLine("Hello, World!");

//var input = @"C:\Users\Hendrik\Downloads\de.csv";
var input = @"C:\Users\Hendrik\Downloads\DAWB_words1.txt";

IpaRhymeList words;

using (var wordsBuilder = new IpaRhymeList.Builder<IWordIpa>())
using (var reader = new StreamReader(input))
{
	string? line;
	while ((line = reader.ReadLine()) != null)
	{
		var split = line.Split(';', 2);
		if (split.Length < 2)
			continue;

		var key = split[0];
		var value = split[1].Trim('/');

		if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
			continue;

		wordsBuilder.TryAdd(new IpaWord(key, value));
	}

	words = wordsBuilder.Build();
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