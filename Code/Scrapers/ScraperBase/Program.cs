using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScraperBase;

internal class Program
{
	private static void Main(string[] args)
	{
		var list = ReadRhymeList();

		var after = GC.GetTotalMemory(true);

		Console.WriteLine("Gelesen!");

		//var rhymeWords = 0;
		//var syllables = 0;
		//var syllablesOrComponents = 0;
		//var problemWords = 0;
		//foreach (var word in list)
		//{
		//	if (word.GetIpaRhymes().Any())
		//		rhymeWords++;
		//	if (word.GetSuffixRhymeGroups())
		//	{
		//		syllables++;
		//		syllablesOrComponents++;
		//	}
		//	else if (word.Components != 0)
		//	{
		//		syllablesOrComponents++;
		//	}
		//	else if (word.Rhymes.Length == 0)
		//	{
		//		problemWords++;
		//	}
		//}

		var line = Console.ReadLine();
		while (line is not null)
		{
			var result = list.Find(line);
			if (result is null)
			{
				Console.WriteLine("Nicht gefunden!");
				line = Console.ReadLine();
				continue;
			}

			var hyphenation = result.Value.Word.GetHyphenation();
			var hyphenated = string.Join('-', result.Value.Word.Hyphenate());
			Console.WriteLine(hyphenated);
			Console.WriteLine("---------------------------");

			Console.WriteLine("IPA:");
			Console.WriteLine(string.Join(", ", result.Value.GetIpaRhymes()));
			Console.WriteLine("---------------------------");

			Console.WriteLine("Einsilbig:");
			Console.WriteLine(string.Join(", ", result.Value.GetSuffixRhymes(1)));
			Console.WriteLine("---------------------------");

			if (hyphenation.Positions.Length >= 1)
			{
				Console.WriteLine("Zweisilbig:");
				Console.WriteLine(string.Join(", ", result.Value.GetSuffixRhymes(2)));
				Console.WriteLine("---------------------------");

				if (hyphenation.Positions.Length >= 2)
				{
					Console.WriteLine("Dreisilbig:");
					Console.WriteLine(string.Join(", ", result.Value.GetSuffixRhymes(3)));
					Console.WriteLine("---------------------------");
				}
			}

			Console.WriteLine();
			line = Console.ReadLine();
		}
	}

	private static RhymeList ReadRhymeList()
	{
		var listBuilder = new RhymeList.Builder();
		var longestLength = 0;
		var longest = string.Empty;
		var path = @"C:\Users\Hendrik\Downloads\dwds-output-binary.txt.gz";
		using (var fileStream = File.OpenRead(path))
		using (var readerStream = new GZipStream(fileStream, CompressionMode.Decompress))
		using (var reader = new BinaryReader(readerStream))
		{
			var step = 100f / fileStream.Length;
			var stride = 100;
			var next = stride;

			while (fileStream.Position < fileStream.Length || reader.PeekChar() >= 0)
			{
				var word = WordInfo.ReadBinary(reader);
				//tree.AddWord(word);
				if (word.DefaultForm is not null && !word.DefaultForm.Text.Contains(' '))
				{
					listBuilder.Add(word);
					if (word.DefaultForm.Text.Length > longestLength)
					{
						longestLength = word.DefaultForm.Text.Length;
						longest = word.DefaultForm.Text;
					}
				}

				next--;
				if (next <= 0)
				{
					next = stride;
					Console.WriteLine($"{fileStream.Position * step}% {fileStream.Position}/{fileStream.Length} ({word.DefaultForm?.Text})");
				}
			}
		}

		return listBuilder.Build();
	}
}
