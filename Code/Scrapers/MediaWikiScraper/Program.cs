using System.Collections;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using MediaWikiScraper;
using ScraperBase;

internal class Program
{
	private static readonly Regex doubleBrackets = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);

	private static readonly Regex overviewRegex = new(@"(\w+).+Übersicht");
	private static readonly Regex overviewFormsRegex = new(@"\|(?!Genus)([\w\*\,\s]{3,})=([^\|]*)$", RegexOptions.Compiled);

	private const string hyphenationStart = ":";
	private static readonly Regex hyphenationRegex = new(@"(?:^|\s)(?:\{\{([^\}]+)\}\})?\s*([^,\{\}]+(?<!\s))", RegexOptions.Compiled);

	private const string rhymeStart = ":{{Reime}}";
	private static readonly Regex rhymeRegex = new(@"\{\{Reim\|([^\}\|]+)(?:\|([^\}\|]+))(\|[^\}]*)?\}\}", RegexOptions.Compiled);

	private static void Main(string[] args)
	{
		/*Console.WriteLine("Dateipfad (.xml):");
		var path = Console.ReadLine();
		if (path is null)
			return;*/

		var pageLinks = new Dictionary<int, int>();
		var antiPageLinks = new Dictionary<int, int>();
		{
			var linksPath = @"C:\Users\Hendrik\Downloads\dewiktionary-20241101-pagelinks.sql.gz";
			using (var fileStream = File.OpenRead(linksPath))
			using (var reader = new StreamReader(new GZipStream(fileStream, CompressionMode.Decompress)))
			{
				var prefix = "INSERT INTO `pagelinks` VALUES ";
				string? line;
				while ((line = reader.ReadLine()) is not null)
				{
					if (!line.StartsWith(prefix))
						continue;

					var values = line[prefix.Length..^1].Split("),(");
					foreach (var value in values)
					{
						var parts = value.Split(',');
						if (parts.Length < 3)
							continue;
						var from = parts[0];
						var to = parts[2];

						if (!int.TryParse(from, out var fromInt)
							|| !int.TryParse(to, out var toInt))
							continue;

						if (!pageLinks.TryGetValue(toInt, out var current))
							current = 0;
						pageLinks[toInt] = current + 1;

						if (!antiPageLinks.TryGetValue(fromInt, out current))
							current = 0;
						antiPageLinks[fromInt] = current + 1;
					}
				}
			}
		}

		var articlesPath = @"C:\Users\Hendrik\Downloads\dewiktionary-20241101-pages-articles.xml";
		//var path = @"C:\Users\Hendrik\Downloads\blume-test.xml";
		var output = @"C:\Users\Hendrik\Downloads\output-binary.txt.gz";

		if (articlesPath.StartsWith('"') && articlesPath.EndsWith('"'))
			articlesPath = articlesPath[1..^1];

		var articleSerializer = new XmlSerializer(typeof(ArticleData));
		var wordSerializer = new XmlSerializer(typeof(WordInfo));

		using (var writerFileStream = File.Open(output, FileMode.Create, FileAccess.Write))
		using (var binaryWriter = new BinaryWriter(new GZipStream(writerFileStream, CompressionLevel.Optimal, leaveOpen: true), Encoding.UTF8))
		{
			var jsonStart = Encoding.UTF8.GetBytes("[\n");
			var jsonDelimiter = Encoding.UTF8.GetBytes(",\n");
			var jsonEnd = Encoding.UTF8.GetBytes("\n]");
			var jsonOptions = new JsonSerializerOptions
			{
				WriteIndented = false,
				DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers = { DefaultValueModifier },
				},
			};
			var wordTypeInfo = jsonOptions.TypeInfoResolver.GetTypeInfo(typeof(WordInfo), jsonOptions)
				?? throw new InvalidOperationException("Error resolving JSON type");

			//writerStream.Write(jsonStart);

			using (var readerStream = File.OpenRead(articlesPath))
			using (var reader = XmlReader.Create(new StreamReader(readerStream, Encoding.UTF8)))
			{
				var step = 100f / readerStream.Length;
				var stride = 1000;
				var next = stride;

				while (!reader.EOF)
				{
					while (reader.NodeType != XmlNodeType.Element || reader.Name != "page")
						if (!reader.Read())
							break;

					var article = articleSerializer.Deserialize(reader) as ArticleData;
					if (article is null || article.Title is null || article.Title.Contains(':'))
						continue;

					var articleLinks = pageLinks.TryGetValue(article.Id, out var links) ? links : 0;
					var articleAntiLinks = antiPageLinks.TryGetValue(article.Id, out var antiLinks) ? antiLinks : 0;

					var word = new WordInfo(article.Title)
					{
						Popularity = articleLinks,
						AntiPopularity = articleAntiLinks,
					};
					var hyphenations = false;
					var rhymes = false;
					foreach (var section in article.EnumerateSections())
					{
						if (!section.Closed)
						{
							ParseForms(word, section);
							continue;
						}

						switch (section.Title)
						{
							case "Worttrennung":
								hyphenations |= ParseHyphenation(word, section);
								break;
							case "Aussprache":
								rhymes |= ParsePronunciation(word, section);
								break;
						}
					}

					if (!hyphenations && !rhymes)
						continue;

					word.WriteBinary(binaryWriter);
					//JsonSerializer.Serialize(writerStream, word, wordTypeInfo);
					//writerStream.Write(jsonDelimiter);
					//wordSerializer.Serialize(writer, word);
					//writer.WriteLine();
					//writer.WriteLine();

					next--;
					if (next <= 0)
					{
						next = stride;
						Console.WriteLine($"{readerStream.Position * step}% {readerStream.Position}/{readerStream.Length} ({article.Title})");
					}
				}
			}

			//writerStream.Write(jsonEnd);

			binaryWriter.Flush();
			writerFileStream.Flush();
			binaryWriter.Close();
			writerFileStream.Close();

			Console.WriteLine("Fertig!");
			Console.ReadLine();
		}
	}

	static bool ParseForms(WordInfo word, ArticleData.Section section)
	{
		var lineMatch = overviewRegex.Match(section.Title);
		if (!lineMatch.Success)
			return false;

		var language = lineMatch.Groups[1].Value;
		if (language != "Deutsch")
			return false;

		foreach (var line in section.ReadLines())
		{
			var match = overviewFormsRegex.Match(line);
			if (!match.Success)
				continue;

			var label = match.Groups[1].Value;
			if (label == string.Empty)
				label = null;
			var text = match.Groups[2].Value;
			if (text == string.Empty)
				continue;

			word.AddForm(text, label);
		}

		return true;
	}

	static bool ParseHyphenation(WordInfo word, ArticleData.Section section)
	{
		var found = false;
		foreach (var line in section.ReadLines())
			if (line.StartsWith(hyphenationStart))
			{
				var matches = hyphenationRegex.Matches(line[hyphenationStart.Length..]);
				if (matches.Count == 0)
					continue;

				foreach (Match match in matches)
				{
					var label = match.Groups[1].Value;
					if (label == string.Empty)
						label = null;
					var value = match.Groups[2].Value;

					word.AddHyphenation(value, label);
					found = true;
				}
			}

		return found;
	}

	static bool ParsePronunciation(WordInfo word, ArticleData.Section section)
	{
		var found = false;
		foreach (var line in section.ReadLines())
		{
			if (!line.StartsWith(rhymeStart))
				continue;

			var matches = rhymeRegex.Matches(line);
			if (matches.Count == 0)
				continue;

			foreach (Match match in matches)
			{
				var language = match.Groups[2].Value;
				if (language == string.Empty)
					language = null;
				var value = match.Groups[1].Value;

				word.AddRhyme(value, language);
				found = true;
			}
		}

		return found;
	}

	static void DefaultValueModifier(JsonTypeInfo typeInfo)
	{
		foreach (var property in typeInfo.Properties)
			if (typeof(ICollection).IsAssignableFrom(property.PropertyType))
				property.ShouldSerialize = (_, val) => val is ICollection collection && collection.Count > 0;
	}
}