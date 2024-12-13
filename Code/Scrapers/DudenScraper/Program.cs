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

Regex accentRegex = new(@"(\p{L})([`´^~˚])", RegexOptions.Compiled);
Regex ipaRegex = new(@"^\[([^\];\s]+)[\];]", RegexOptions.Compiled);

var input = @"C:\Users\Hendrik\Downloads\Duden - Deutsches Universalwörterbuch_removed.pdf";
var output = @"C:\Users\Hendrik\Downloads\Duden-words1.txt";

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
		var ipaMatch = ipaRegex.Match(currentNonWord.ToString());
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
		if (word.EndsWith(':'))
			word = word[..^1];

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

		if (ipa is not null)
		{
			writer.Write('\t');
			writer.Write(ipa);
		}

		writer.WriteLine();
	}

	currentWord.Clear();
}