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
		var tree = new RhymeTree();
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
				tree.AddWord(word);

				next--;
				if (next <= 0)
				{
					next = stride;
					Console.WriteLine($"{fileStream.Position * step}% {fileStream.Position}/{fileStream.Length} ({word.DefaultForm?.Text})");
				}
			}
		}

		var before = GC.GetTotalMemory(true);

		tree.Finish();

		var after = GC.GetTotalMemory(true);

		Console.WriteLine("Gelesen!");

		var line = Console.ReadLine();
		while (line is not null)
		{
			var rhymes = tree.GetRhymes(line);
			Console.WriteLine(string.Join(", ", rhymes.Rhymes));
			Console.WriteLine("---------------------------");
			Console.WriteLine(string.Join(", ", rhymes.HyphenRhymes));
			Console.WriteLine();
			line = Console.ReadLine();
		}
	}
}
