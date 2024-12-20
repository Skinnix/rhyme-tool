﻿using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DwdsScraper;
using HtmlAgilityPack;
using ScraperBase;
using ZimReaderSharp;

internal class Program
{
	static Regex ipaRegex = new(@"(?:^|\[)([^\[\]]+)(?:\]|$)", RegexOptions.Compiled);

	private static void Main0(string[] args)
	{
		Console.WriteLine("Hello, World!");

		var zimFile = new ZimFile(@"C:\Users\Hendrik\Downloads\dwds_de_dictionary_nopic_2024-11-15.zim");

		var output = @"C:\Users\Hendrik\Downloads\dwds-output-compressed.bin.gz";

		using (var writerFileStream = File.Open(output, FileMode.Create, FileAccess.Write))
		using (var binaryWriter = new BinaryWriter(new GZipStream(writerFileStream, CompressionLevel.Optimal, leaveOpen: true), Encoding.UTF8))
		{
			var stride = 100;
			var next = stride;
			var document = new HtmlDocument();
			foreach (var (index, mime) in zimFile.IterateArticles())
			{
				if (mime != "text/html")
					continue;

				var entry = zimFile.GetArticleByIndex(index, fFollowRedirect: false);
				if (entry.IsRedirect || entry.Format.Url?.StartsWith("wb/") != true
					|| entry.Format.Url.Length < 4 || !char.IsLetter(entry.Format.Url[3]))
					continue;

				if (entry.Data is null)
					continue;

				var html = Encoding.UTF8.GetString(entry.Data);
				document.LoadHtml(html);
				var article = document.DocumentNode.SelectSingleNode(".//div[@class='dwdswb-ft']");
				if (article is null)
					continue;

				var compressed = CompressArticle(article);
				if (compressed is null)
					continue;

				compressed.WriteBinary(binaryWriter);

				if (--next <= 0)
				{
					next = stride;
					Console.WriteLine($"{index * 100 / zimFile.TotalArticles}% {index}/{zimFile.TotalArticles}: {compressed.Title}");
				}
			}

			binaryWriter.Flush();
			writerFileStream.Flush();
			binaryWriter.Close();
			writerFileStream.Close();

			Console.WriteLine("Fertig!");
			Console.ReadLine();
		}
	}

	private static void Main(string[] args)
	{
		Console.WriteLine("Hello, World!");

		var input = @"C:\Users\Hendrik\Downloads\dwds-output-compressed.bin.gz";
		var output = @"C:\Users\Hendrik\Downloads\dwds-output-binary.txt.gz";

		using (var readerFileStream = File.OpenRead(input))
		using (var binaryReader = new BinaryReader(new GZipStream(readerFileStream, CompressionMode.Decompress, leaveOpen: true), Encoding.UTF8))
		using (var writerFileStream = File.Open(output, FileMode.Create, FileAccess.Write))
		using (var binaryWriter = new BinaryWriter(new GZipStream(writerFileStream, CompressionLevel.Optimal, leaveOpen: true), Encoding.UTF8))
		{
			var stride = 100;
			var next = stride;
			var read = 0;
			var document = new HtmlDocument();
			do
			{
				CompressedArticle? compressed = null;
				try
				{
					compressed = CompressedArticle.ReadBinary(binaryReader);
				}
				catch (EndOfStreamException)
				{
					break;
				}

				var html = $"<div class=\"dwdswb-ft-blocks\">{compressed.Content}</div>";
				document.LoadHtml(html);

				var word = ReadArticle(document.DocumentNode, compressed.Title);
				if (word is null)
					continue;

				word.WriteBinary(binaryWriter);
				read++;
				if (--next <= 0)
				{
					next = stride;
					Console.WriteLine($"{read}: {word.DefaultForm!.Text}");
				}
			}
			while (true);

			binaryWriter.Flush();
			writerFileStream.Flush();
			binaryWriter.Close();
			writerFileStream.Close();

			Console.WriteLine("Fertig!");
			Console.ReadLine();
		}
	}

	private static void Main2(string[] args)
	{
		Console.WriteLine("Hello, World!");

		var zimFile = new ZimFile(@"C:\Users\Hendrik\Downloads\dwds_de_dictionary_nopic_2024-11-15.zim");

		var output = @"C:\Users\Hendrik\Downloads\dwds-output-binary.txt.gz";

		using (var writerFileStream = File.Open(output, FileMode.Create, FileAccess.Write))
		using (var binaryWriter = new BinaryWriter(new GZipStream(writerFileStream, CompressionLevel.Optimal, leaveOpen: true), Encoding.UTF8))
		{
			var stride = 100;
			var next = stride;
			var document = new HtmlDocument();
			foreach (var (index, mime) in zimFile.IterateArticles())
			{
				if (mime != "text/html")
					continue;

				var entry = zimFile.GetArticleByIndex(index, fFollowRedirect: false);
				if (entry.IsRedirect || entry.Format.Url?.StartsWith("wb/") != true
					|| entry.Format.Url.Length < 4 || !char.IsLetter(entry.Format.Url[3]))
					continue;

				if (entry.Data is null)
					continue;

				var html = Encoding.UTF8.GetString(entry.Data);
				document.LoadHtml(html);
				var article = document.DocumentNode.SelectSingleNode(".//div[@class='dwdswb-ft']");
				if (article is null)
					continue;

				var word = ReadArticle(article);
				if (word is null)
					continue;

				word.WriteBinary(binaryWriter);

				if (--next <= 0)
				{
					next = stride;
					Console.WriteLine($"{index * 100 / zimFile.TotalArticles}% {index}/{zimFile.TotalArticles}: {word.DefaultForm!.Text}");
				}
			}

			binaryWriter.Flush();
			writerFileStream.Flush();
			binaryWriter.Close();
			writerFileStream.Close();

			Console.WriteLine("Fertig!");
			Console.ReadLine();
		}
	}

	private static CompressedArticle? CompressArticle(HtmlNode article)
	{
		var title = article.SelectSingleNode(".//*[@class='dwdswb-ft-lemmaansatz']/b")?.InnerText;
		var blockWrapper = article.SelectSingleNode(".//div[@class='dwdswb-ft-blocks']");
		if (title is null || blockWrapper is null)
			return null;

		if (title is null || blockWrapper is null)
			return null;

		if (title.Contains(' ')) //Keine Sätze
			return null;

		return new(title, blockWrapper.InnerHtml);
	}

	private static WordInfo? ReadArticle(HtmlNode article, string? title = null)
	{
		title ??= article.SelectSingleNode(".//*[@class='dwdswb-ft-lemmaansatz']/b")?.InnerText;

		var activeNodes = article.SelectNodes("//*[@class='word-frequency-active']");
		var inactiveNodes = article.SelectNodes("//*[@class='word-frequency-inactive']");
		var popularity = -1;
		if (activeNodes is not null && inactiveNodes is not null)
		{
			var active = activeNodes.Count;
			var inactive = inactiveNodes.Count;
			var total = active + inactive;
			popularity = (int)Math.Round(active / (double)total * 100);
		}

		var blockWrapper = article.SelectSingleNode(".//div[@class='dwdswb-ft-blocks']");
		if (title is null || blockWrapper is null)
			return null;

		if (title.Contains(' ')) //Keine Sätze
			return null;

		var word = new WordInfo(title);
		foreach (var block in blockWrapper.ChildNodes)
		{
			var blockLabel = block.SelectSingleNode(".//*[@class='dwdswb-ft-blocklabel serif italic']")?.InnerText.Trim();
			var blockContent = block.SelectSingleNode(".//*[@class='dwdswb-ft-blocktext']");
			switch (blockLabel)
			{
				case "Grammatik" or "Nebenform" or "Alternative Schreibungen":
					var formNodes = blockContent.SelectNodes(".//b");
					if (formNodes is null)
						continue;

					foreach (var formNode in formNodes)
					{
						var form = formNode.InnerText;
						word.AddForm(form, null);
					}
					break;
				case "Grundform":
					var baseForm = blockContent.SelectSingleNode(".//a");
					if (baseForm is null)
						continue;
					word.BaseForm = baseForm.InnerText;
					break;
				case "Aussprache":
					var ipaNodes = blockContent.SelectNodes(".//*[@class='dwdswb-ipa']");
					if (ipaNodes is null)
						continue;

					foreach (var ipaNode in ipaNodes)
					{
						var pronunciation = ipaNode.InnerText;
						foreach (Match match in ipaRegex.Matches(pronunciation))
						{
							if (!match.Success)
								continue;

							var ipa = match.Groups[1].Value;
							word.AddRhyme(ipa, null);
						}
					}
					break;
				case "Worttrennung":
					var hyphenationNodes = blockContent.SelectNodes(".//*[@class='hyphenation']");
					if (hyphenationNodes is null)
						continue;

					foreach (var hyphenationNode in hyphenationNodes)
					{
						var hyphenations = hyphenationNode.InnerText.Split(['·', '●'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
						foreach (var hyphenation in hyphenations)
						{
							var cleanHyphenation = hyphenation.Replace("-", WordInfo.HYPHENATION_SEPARATOR);
							word.AddHyphenation(cleanHyphenation, null);
						}
					}
					break;
				case "Wortzerlegung":
					var decompositionNodes = blockContent.SelectNodes(".//a");
					if (decompositionNodes is null || decompositionNodes.Count == 0)
						continue;

					foreach (var componentNode in decompositionNodes)
					{
						var component = componentNode.GetDirectInnerText();
						word.DefaultForm!.Components.Add(component);
					}
					break;
			}
		}

		return word;
	}
}