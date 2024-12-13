// See https://aka.ms/new-console-template for more information
using System.Text;
using System.Text.RegularExpressions;
using DudenScraper;
using HtmlAgilityPack;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using PdfSharp.Snippets.Font;

using GraphicsState = (bool CurrentlyBlue, bool WrittenBlue, bool WrittenHyphen, bool JustStroked, bool AlreadyWrittenInLine);

const char STRESS1 = '§';
const char STRESS2 = '$';

var ipa_count = 0;

Regex accentRegex = new(@"(\p{L})([`´^~˚])", RegexOptions.Compiled);
Regex ipaRegex = new(@"^\[(?!auch:)(?!griech\.)(?!H\.u\.)([^\];\s\u0004]+)[\];]", RegexOptions.Compiled);

Dictionary<string, string> ipaConversion = new()
{
	{ "\a", "ː" },
	{ "\u0005", "ɛ" },
	{ "\b", "ˈ" },
	{ "\u000e", "ʔ" },
	{ "\u0016", "ʊ" },
	{ "\u0015", "ɑ" },
	{ "\u000f", "ɔ" },
	{ "\u001f", "̃" },
	{ "\u0006", "ʒ" },
	{ "\u0014", "ɪ" },
	{ "\u0011", "̯" },
	{ "\u009c", "œ" },
	{ "\u0012", "ɐ" },
	{ "\u0013", "ɡ" },
	{ "æ", "æ" },
	{ "\u0017", "ŋ" },
	{ "ø", "ø" },
	{ "w", "w" },
	{ "\u001a", "ʌ" },
	{ "\u001c", "θ" },
	{ "\u0019", "ʏ" },
	{ "\u001b", "ð" },
	{ "\u0018", "\u0306" },
	{ "\u0080", "ɲ" },

	{ "aè", "aː" },
	{ "", "ɐ" },
	{ "í", "ɐ̯" },
	{ "³˜", "ɑ̃" },
	{ "³˜è", "ɑ̃ː" },
	{ "a¹í", "aɪ̯" },
	{ "aí", "aʊ̯" },
	{ "c¸", "ç" },
	{ "då", "dʒ" },
	{ "eè", "eː" },
	{ "¸", "ɛ" },
	{ "¸è", "ɛː" },
	{ "˜¸", "ɛ̃" },
	{ "˜¸è", "ɛ̃ː" },
	{ "·", "ə" },
	{ "·í", "ə̯" },
	{ "", "ɡ" },
	{ "iè", "iː" },
	{ "ií", "i̯" },
	{ "¹", "ɪ" },
	{ "lì", "l̩" },
	{ "mì", "m̩" },
	{ "nì", "n̩" },
	{ "½", "ŋ" },
	{ "oè", "oː" },
	{ "oí", "o̯" },
	{ "¶˜", "ɔ̃" },
	{ "¶˜è", "ɔ̃ː" },
	{ "¶", "ɔ" },
	{ "øè", "øː" },
	{ "œ˜", "œ̃" },
	{ "¶¹í", "ɔɪ̯" },
	{ "pf", "pf" },
	{ "r", "ʀ" },
	{ "Ã", "ʃ" },
	{ "ts", "ts" },
	{ "tÃ", "tʃ" },
	{ "uè", "uː" },
	{ "uí", "u̯" },
	{ "", "ʊ" },
	{ "yè", "yː" },
	{ "y˘", "y̆" },
	{ "¡", "ʏ" },
	{ "å", "ʒ" },
	{ "³è", "ɑː" },
	{ "", "ʌ" },
	{ "¸¹í", "ɛɪ̯" },
	{ "¶í", "ɔʊ̯" },
	{ "", "ð" },
	{ "Å", "θ" },
	{ "Ì", "β" },
	{ "»", "ʔ" },
	{ "è", "\u02D0" },
	{ "~", "\u0303" },
	{ "±", "\u02C8" },
	{ "", "\u0329" },
	{ "í", "\u032F" },
	{ "˘", "\u0306" },
};
var ipaCharacters = ipaConversion.Values.Concat("abcdefghijklmnopqrstuvwxyz().,-".ToCharArray().Select(c => c.ToString())).Distinct().ToArray();

var input = @"C:\Users\Hendrik\Downloads\Duden\Duden - Deutsches Universalwörterbuch_removed.pdf";
var output = @"C:\Users\Hendrik\Downloads\Duden\Duden-words1.txt";

var document = PdfReader.Open(input, PdfDocumentOpenMode.Import);

using (var file = File.Open(output, FileMode.Create))
using (var writer = new StreamWriter(file))
{
	var i = 0;
	foreach (var page in document.Pages)
	{
		Console.WriteLine("Page " + i++);
		var sequence = ContentReader.ReadContent(page);

		var currentWord = new StringBuilder();
		var currentNonWord = new StringBuilder();

		GraphicsState graphicsState = default;
		var graphicsStack = new Stack<GraphicsState>();
		foreach (var element in sequence)
		{
			if (element is COperator cOperator)
			{
				switch (cOperator.OpCode.Name)
				{
					case "cs" or "CS" or "scn" or "SCN":
						graphicsState.CurrentlyBlue = true;
						break;
					case "S":
						graphicsState.JustStroked = true;
						break;
					case "k":
						graphicsState.CurrentlyBlue = false;
						break;
					case "Tj" or "TJ":
						var text = string.Join(null, cOperator.ExtractText());

						var ignoreBlue = false;
						if (graphicsState.CurrentlyBlue)
						{
							var checkText = text;
							if (checkText.EndsWith('.'))
								checkText = checkText[..^1];

							if (!graphicsState.AlreadyWrittenInLine && int.TryParse(checkText, out _))
								ignoreBlue = true;
						}

						if (text == "\x001b")
							text = STRESS2.ToString();
						
						if (text.Contains('¯'))
							text = text.Replace('¯', STRESS1);

						if (text.Contains('\x001e'))
							text = text.Replace('\x001e', '˚');

						//if (text.Any(char.IsControl))
						//	text = new string(text.Where(c => !char.IsControl(c)).ToArray());

						//Console.ForegroundColor = graphicsState.CurrentlyBlue && !ignoreBlue ? ConsoleColor.Blue : ConsoleColor.White;
						//Console.BackgroundColor = graphicsState.CurrentlyBlue && !ignoreBlue && graphicsState.JustStroked ? ConsoleColor.Cyan : ConsoleColor.Black;
						//Console.Write(text);

						graphicsState.WrittenHyphen = text == "-";

						if (graphicsState.CurrentlyBlue && !ignoreBlue)
						{
							if (graphicsState.JustStroked)
								currentWord.Append(STRESS1);

							currentWord.Append(text);
							currentNonWord.Clear();
						}
						else
						{
							currentNonWord.Append(text);
						}

						graphicsState.AlreadyWrittenInLine = true;
						graphicsState.JustStroked = false;
						graphicsState.WrittenBlue = graphicsState.CurrentlyBlue;
						break;
					case "Tm":
						if (graphicsState.WrittenBlue)
						{
							if (graphicsState.WrittenHyphen)
								currentWord.Remove(currentWord.Length - 1, 1);

							//Console.Write("\b");
							break;
						}
						if (!graphicsState.AlreadyWrittenInLine)
							break;
						graphicsState.AlreadyWrittenInLine = false;

						WriteWord(writer, currentWord, currentNonWord);

						//Console.WriteLine();
						break;
					case "q":
						graphicsStack.Push(graphicsState);
						break;
					case "Q":
						graphicsState = graphicsStack.Pop();
						break;
				}
			}
		}

		WriteWord(writer, currentWord, currentNonWord);
	}

	writer.Flush();
	writer.Close();
}

Console.WriteLine("Fertig!");

void WriteWord(StreamWriter writer, StringBuilder currentWord, StringBuilder currentNonWord)
{
	string? ipa = null;
	if (currentNonWord.Length > 0)
	{
		var ipaMatch = ipaRegex.Match(currentNonWord.ToString().Replace("[\u000e]", "(\u000e)"));
		if (ipaMatch.Success)
		{
			ipa = ipaMatch.Groups[1].Value;
			currentNonWord.Clear();
		}
	}

	var words = currentWord.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	foreach (var w in words)
	{
		var word = w;
		while (word.EndsWith(':') || word.EndsWith(';') || word.EndsWith('Y'))
			word = word[..^1];
		word = word.Replace("\u0002", string.Empty);

		if (word.StartsWith('-') || word.EndsWith('-'))
			continue;

		word = accentRegex.Replace(word, m =>
		{
			var letter = m.Groups[1].Value;
			var accent = m.Groups[2].Value;

			return accent switch
			{
				"`" => (letter + "\u0300").Normalize(),
				"´" => (letter + "\u0301").Normalize(),
				"^" => (letter + "\u0302").Normalize(),
				"~" => (letter + "\u0303").Normalize(),
				"˚" or "\u001e" => (letter + "\u030A").Normalize(),
				_ => throw new ArgumentException("Unbekannter Akzent: " + accent)
			};
		});
		
		writer.Write(word);

		if (ipa is not null && !ipa.Contains("..."))
		{
			writer.Write('\t');
			var converted = ipa;
			foreach (var (key, value) in ipaConversion)
				converted = converted.Replace(key, value);
			/*if (!converted.All(c => ipaCharacters.Contains(c.ToString())))
			{

			}*/
			writer.Write(converted);

			ipa_count++;
		}

		writer.WriteLine();
	}

	currentWord.Clear();
}