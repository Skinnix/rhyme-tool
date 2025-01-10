// See https://aka.ms/new-console-template for more information
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DudenScraper;
using HtmlAgilityPack;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using PdfSharp.Snippets.Font;

using GraphicsState = (string? Font, bool WritingWord, (double ScaleX, double ShearX, double ShearY, double ScaleY, double OffsetX, double OffsetY) Matrix);

const char IPA_SEPARATOR = '$';
const char ALTERNATIVE_IPA_SEPARATOR = '§';

Regex languageOrParenthesesRegex = new(
	@"(?:^\s*(?<parens>(?<open>\()?(?<comment>[^\)]+)?(?<close>\))?)??\s*(?<lang>(?<langName>[\w\-\.\/]+)(?:(\s?(?<dot>\.))|(?<=(isch|thai|hindi|engl|russ|fr|urdu))))?$)",
	RegexOptions.Compiled);

Regex ignoreIpaRegex = new(
	@"(?:^\[)|(?:\]\.?$)",
	RegexOptions.Compiled);

var ipa_count = 0;

Dictionary<string, string> ipaConversion = new()
{
	{ "[", "[" },
	{ "]", "}" },

	{ ":", "ː" },
	{ "~", "\u0303" },
	{ "\"", "ˈ" },
	{ "%", "ˌ" },
	{ " ", " " },
	{ "ß", "\u0325" }, //◌̥
	{ "á", "\u030A" }, //◌̊
	{ "^", "\u032F" }, //◌̯
	{ "â", "\u0306" }, //◌̆
	{ "+", "\u0329" }, //◌̩
	{ ">", "\u0329" }, //◌̩
	{ "ÿ", "\u035C" }, //◌͜◌
	{ "þ", "\u0361" }, //◌͡◌
	{ "ê", "\u030D" }, //◌̍
	{ "Ù", "\u0329" }, //◌̩
	{ "$", "\u032F" }, //◌̯
	{ "§", "\u032F" }, //◌̯
	{ "Ø", "\u0325" }, //◌̥
	{ "ë", "\u032C" }, //◌̬
	{ "ò", "\u0303" }, //◌̃
	{ "?", "ʔ" },

	{ "a", "a" },
	{ "A", "ɑ" },
	{ "6", "ɐ" },
	{ "ó", "1ɐ" }, //hochgestelltes ɐ

	{ "e", "e" },
	{ "E", "ɛ" },
	{ "@", "ə" },

	{ "i", "i" },
	{ "I", "ɪ" },
	{ "\u0080", "ɪ" },
	{ "ö", "i̯" },

	{ "o", "o" },
	{ "2", "ø" },
	{ "O", "ɔ" },
	{ "9", "œ" },

	{ "u", "u" },
	{ "U", "ʊ" },

	{ "y", "y" },
	{ "Y", "ʏ" },

	{ "b", "b" },
	{ "c", "c" },
	{ "C", "ç" },
	{ "d", "d" },
	{ "D", "ð" },
	{ "f", "f" },
	{ "g", "ɡ" },
	{ "G", "ɣ" },
	{ "h", "h" },
	{ "j", "j" },
	{ "\u009d", "ʝ" },
	{ "`", "ʝ̊".Normalize() },
	{ "k", "k" },
	{ "l", "l" },
	{ "m", "m" },
	{ "n", "n" },
	{ "N", "ŋ" },
	{ "©", "ɳ" },
	{ "p", "p" },
	{ "õ", "1ʁ" }, //hochgestelltes ʁ
	{ "r", "r" },
	{ "R", "ʁ" },
	{ "s", "s" },
	{ "S", "ʃ" },
	{ "\u0089", "" }, //unbekannt
	{ "t", "t" },
	{ "T", "θ" },
	{ "v", "v" },
	{ "w", "w" },
	{ "x", "x" },
	{ "z", "z" },
	{ "Z", "ʒ" },

	{ ".", "." }, //...
	{ "-", string.Empty }, //Silbentrennung
};
Dictionary<string, string> specialCharacterConversion = new()
{
	{ "z", "z" },
	{ "a", "a" },
	{ "e", "e" },

	{ "ù", "\u0306" },
	{ "\u009a", "ı" },
	{ "­", " Ł" },
	{ "·", "ł" },
	{ "\u0014", "ł" },
	{ "ý", "̋" },
	{ "ø", "\u0304" },
	{ "þ", "˛" },
	{ "ú", "\u0307" },
	{ "û", "\u030A" },
	{ "¸", "\u0327" },
	{ "\u0019", "\u030C" },
	{ "ˇ", "\u030C" },
	{ "´", "\u0301" },
	{ "`", "\u0300" },
	{ "\u0083", "..." },
	{ "˛", "˛" },
	{ "\u001A", "\u0307" },
	{ "\u0017", "\u0306" },
	{ "\u008E", "\u030B" },
	{ "\u001C", "\u0328" },
	{ "\u001B", "\u030B" },
	{ "¨", "®" },
	{ "¯", "\u0304" },
	{ "\u0001", "\u0326" },

	{ "o", "o" },
	{ "õ", "i" },
};
List<KeyValuePair<string, string>> specialCharacterRepair = new()
{
	new(" \u0306g", "ğ"),
	new("\u0306g", "ğ"),
	new("\u009a", "ı"),
	new("̋ o", "ő"),
	new("̋o", "ő"),
	new("̋ O", "Ő"),
	new("̋O", "Ő"),
	new("\u030Bo", "ő"),
	new(" \u0304u", "ū"),
	new("\u0304u", "ū"),
	new("\u0307z", "ż"),
	new("\u0306a", "ă"),

	new("\u0304i", "ī"),

	new("˛e", "ę"),
	new("\u0307z", "ż"),
	new("\u030Au", "ů"),
	new("sˇ", "š"),
	new("s\u0301", "ś"),
	new("cˇ", "č"),
	new("eˇ", "ě"),
	new("y´", "ý"),
	new("c´", "ć"),
	new("o˛", "ớ"),
	new("\u0328", "ę"),
	new("\u0304e", "ē"),
	new("øe", "ē"),
	new("T\u0326", "Ț"),
	new("D -", "Đ"),
};
var ipaCharacters = ipaConversion.Values.Concat("abcdefghijklmnopqrstuvwxyz().,-".ToCharArray().Select(c => c.ToString())).Distinct().ToArray();

var input = @"C:\Users\Hendrik\Downloads\De_Gruyter_Deutsches_Aussprachewoerterbuch_removed.pdf";
var output = @"C:\Users\Hendrik\Downloads\DAWB_words1.txt";

var pdfDocument = PdfReader.Open(input, PdfDocumentOpenMode.Import);

using (var file = File.Open(output, FileMode.Create))
using (var writer = new StreamWriter(file))
{
	var i = 0;
	foreach (var page in pdfDocument.Pages)
	{
		Console.WriteLine("Page " + i++);
		var sequence = ContentReader.ReadContent(page);

		StringBuilder currentWord = new();
		StringBuilder currentComment = new();
		var wasSpecialCharacter = false;
		var movedLeft = false;
		StringBuilder currentIpa = new();

		GraphicsState graphicsState = default;
		var graphicsStack = new Stack<GraphicsState>();
		foreach (var cOperator in sequence.OfType<COperator>())
		{
			switch (cOperator.OpCode.Name)
			{
				case "Tf":
					var fontId = (cOperator.Operands.FirstOrDefault() as CName)?.Name;
					var fontName = GetFontName(page, fontId);
					graphicsState.Font = fontName;
					break;
				case "Tm":
					var operands = cOperator.Operands.Select(o => (o as CReal)?.Value ?? (o as CInteger)?.Value ?? 0).ToArray();
					graphicsState.Matrix = (operands[0], operands[1], operands[2], operands[3], operands[4], operands[5]);
					movedLeft = false;
					break;
				case "TD":
					movedLeft = ((cOperator.Operands[0] as CInteger)?.Value ?? (cOperator.Operands[0] as CReal)?.Value ?? 0) < 0;
					break;
				case "Tj" or "TJ":
					//Ignoriere Seitenheader
					if (graphicsState.Matrix.OffsetY > 660)
						continue;

					//Ignoriere Überschriften
					if (graphicsState.Matrix.ScaleX > 10)
						continue;

					var text = string.Join(null, cOperator.ExtractText());

					var isWord = graphicsState.Font?.Contains("Bold") == true && text != ".";
					//|| (graphicsState.Font == "/F1" && currentWord.Length != 0 && currentIpa.Length == 0 && currentComment.Length == 0 && currentLanguage.Length == 0);
					var isComment = graphicsState.Matrix.ShearX != 0;
					var isIpa = graphicsState.Font?.Contains("Ipasam") == true;
					var isSymbol = graphicsState.Font?.Contains("Minion") == true;
					var isSpecialCharacter = false;
					var isItalic = graphicsState.Matrix.ShearY != 0;
					bool? needsSpace = null;

					//Sonderfälle
					if (i == 139)
					{
						if (text == "Sing")
						{
							isWord = true;
							needsSpace = false;
						}
						else if (text == ".)")
						{
							needsSpace = false;
						}
						else if (text == "Plur.)")
						{
							isWord = true;
							needsSpace = false;
						}
					}
					else if (i == 146)
					{
						if (text == "428")
						{
							isWord = false;
						}
						else if (text == "dabehalten")
						{
							isWord = true;
							currentWord.Clear();
						}
					}
					else if (i == 170)
					{
						if (text == "<")
						{
							text = "ˇ";
							isIpa = false;
							isWord = true;
							needsSpace = false;
							wasSpecialCharacter = true;
						}
					}
					else if (i == 202 || i == 454 || i == 574 || i == 680)
					{
						if (text == "u^ yâ]")
						{
							continue;
						}

						if (isWord && text.StartsWith("A"))
							continue;
					}
					else if (i == 291)
					{
						if (text == "-")
						{
							isWord = false;
							isComment = true;
							isItalic = true;
						}
					}
					else if (i == 397)
					{
						if (text == "<")
						{
							text = "ˇ";
							isIpa = false;
							isWord = true;
							needsSpace = false;
							wasSpecialCharacter = true;
						}
					}
					else if (i == 485)
					{
						if (text == "ç")
						{
							text = "g";
							isIpa = false;
							isWord = true;
							needsSpace = false;
							wasSpecialCharacter = true;
						}
					}
					else if (i == 487)
					{
						if (text == "\u0017")
							continue;
					}
					else if (i == 538)
					{
						if (currentWord.ToString() == "Pharma" && text == ":")
							continue;
					}
					else if (i == 724)
					{
						if (currentWord.ToString() == "Üetli" && text == ":")
							continue;
					}

					if (isWord)
						text = text.Replace("\u0090", "'").Replace("\u009A", "ı").Replace("\u009C", "œ").Replace("\u0019", "ˇ");
					else if (isIpa)
						text = ReplaceCharacters(text, ipaConversion);

					if (i == 783)
					{
						if (text == "au" && currentWord.Length != 0 && currentWord[^1] == 'ˇ')
						{
							currentWord.Remove(currentWord.Length - 1, 1);
							text = "ău";
						}
					}

					if (text.Length == 1 && currentIpa.Length == 0 && char.GetUnicodeCategory(text[^1]) is UnicodeCategory.Control or UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol or UnicodeCategory.NonSpacingMark)
					{
						isSpecialCharacter = true;
					}

					if (text.Length == 1 && isWord && isSpecialCharacter)
					{
						text = ReplaceCharacters(text, specialCharacterConversion);
						wasSpecialCharacter = true;
						isSpecialCharacter = true;
						needsSpace = false;
					}

					if (graphicsState.Font?.Contains("Cn") == true)
					{
						isWord = true;
						isSpecialCharacter = true;

						if (!wasSpecialCharacter)
							text = ReplaceCharacters(text, specialCharacterConversion);
						if (char.IsLetter(text[^1]))
						{
							needsSpace = false;
							wasSpecialCharacter = true;
						}
					}

					if (text == "®" || (isSymbol && text == "¨"))
					{
						continue;
					}

					if (isIpa
						&& currentWord.Length != 0 && currentIpa.Length == 0 && currentComment.Length == 0
						&& text.Length == 1
						&& isSpecialCharacter)
					{
						isIpa = false;
						isWord = true;
						needsSpace = false;
					}

					if (!isWord && !isIpa && currentWord.Length != 0 && currentWord[^1] == '(')
					{
						if (currentWord.Length != 1 && currentWord[^2] == ' ')
							currentWord.Remove(currentWord.Length - 2, 2);
						else
							currentWord.Remove(currentWord.Length - 1, 1);

						currentComment = new("(" + currentComment.ToString());
					}

					if (isWord && text == ")")
					{
						currentComment.Append(")");
						continue;
					}

					if (isWord && text == "-" && currentWord.Length != 0 && currentWord[^1] == 'D')
					{
						currentWord.Remove(currentWord.Length - 1, 1);

						if (currentWord.Length != 0 && currentWord[^1] != ' ')
							currentWord.Append(' ');

						currentWord.Append("Đ");
						wasSpecialCharacter = true;
						continue;
					}

					if (isWord)
					{
						if (currentComment.Length != 0 || currentIpa.Length != 0)
							WriteWord(writer, currentWord, currentComment, currentIpa, graphicsState);
						else if (!isSpecialCharacter && !wasSpecialCharacter)
							needsSpace ??= movedLeft ? false : currentWord.Length != 0 && currentWord[^1] != '-';

						if (needsSpace == true)
							currentWord.Append(' ');
						
						if (needsSpace != true && movedLeft && currentWord.Length != 0 && text.Length == 1)
						{
							var previous = currentWord[^1];
							currentWord.Remove(currentWord.Length - 1, 1);
							currentWord.Append(text);
							currentWord.Append(previous);
						}
						else
						{
							currentWord.Append(text);
						}

						if (wasSpecialCharacter)
						{
							var word = currentWord.ToString();
							foreach (var repair in specialCharacterRepair)
							{
								if (word.EndsWith(repair.Key, StringComparison.Ordinal))
								{
									currentWord.Remove(currentWord.Length - repair.Key.Length, repair.Key.Length);
									currentWord.Append(repair.Value);
									break;
								}
							}
						}
					}
					else if (isIpa)
					{
						if (currentWord.Length != 0)
						{
							graphicsState.WritingWord = false;

							needsSpace ??= currentIpa.Length != 0 && currentIpa[currentIpa.Length - 1] != ALTERNATIVE_IPA_SEPARATOR;

							if (needsSpace == true)
								currentIpa.Append(' ');

							currentIpa.Append(text);
						}
						else if (!ignoreIpaRegex.IsMatch(text))
						{
							throw new InvalidOperationException("Aussprache ohne Wort");
						}
					}
					else if (text == "od" || text == "od." || text == ".")
					{
						if (currentIpa.Length != 0 && currentIpa[currentIpa.Length - 1] != ALTERNATIVE_IPA_SEPARATOR)
							currentIpa.Append(ALTERNATIVE_IPA_SEPARATOR);
					}
					else if (text == "od.\u0083")
					{
						if (currentIpa.Length != 0 && currentIpa[currentIpa.Length - 1] != ALTERNATIVE_IPA_SEPARATOR)
							currentIpa.Append(ALTERNATIVE_IPA_SEPARATOR).Append("...");
					}
					else
					{
						graphicsState.WritingWord = false;

						/*var match = languageOrParenthesesRegex.Match(text);
						var canIgnore = false;
						if (match.Success)
						{
							var parentheses = match.Groups["parens"].Value;
							var open = match.Groups["open"].Success;
							var close = match.Groups["close"].Success;
							if (parentheses.Length != 0
								&& (open || currentComment.Length != 0)
								&& (!close || open || currentComment.Length != 0))
							{
								if (currentComment.Length != 0 && currentComment[currentComment.Length - 1] == '-')
									currentComment.Remove(currentComment.Length - 1, 1);
								else if (currentComment.Length != 0)
									currentComment.Append(' ');

								currentComment.Append(parentheses);
								canIgnore = true;
							}

							var language = match.Groups["langName"].Value + match.Groups["dot"].Value;
							if (language.Length != 0)
							{
								currentLanguage.Append(language);
								canIgnore = true;
							}
						}*/

						var canIgnore = false;
						if (isItalic)
						{
							currentComment.Append(text);
							canIgnore = true;
						}

						if (!canIgnore)
							WriteWord(writer, currentWord,  currentComment, currentIpa, graphicsState);
					}

					wasSpecialCharacter = isSpecialCharacter;
					movedLeft = false;
					break;
				case "q":
					graphicsStack.Push(graphicsState);
					break;
				case "Q":
					graphicsState = graphicsStack.Pop();
					movedLeft = false;
					break;
			}
		}

		WriteWord(writer, currentWord, currentComment, currentIpa, graphicsState);
	}

	writer.Flush();
	writer.Close();
}

static string ReplaceCharacters(string text, IReadOnlyDictionary<string, string> conversion)
{
	var result = new StringBuilder();
	var current = new StringBuilder();

	foreach (var character in text)
	{
		current.Append(character);

		if (conversion.TryGetValue(current.ToString(), out var replacement))
		{
			result.Append(replacement);
			current.Clear();
		}
	}

	if (current.Length != 0)
		result.Append(conversion[current.ToString()]);

	return result.ToString();
}

static void WriteWord(StreamWriter output,
	StringBuilder currentWord, StringBuilder currentComment, StringBuilder currentIpa,
	GraphicsState graphicsState)
{
	if (currentIpa.Length != 0)
	{
		var line = currentWord.ToString().Normalize();
		/*if (currentComment.Length != 0)
			line += $" {currentComment.ToString().Normalize()}";*/
		/*if (currentLanguage.Length != 0)
			line += $" [{currentLanguage.ToString().Normalize()}]";*/
		line += $";{currentIpa.ToString().Normalize()}";
		Console.WriteLine(line);
		output.WriteLine(line);

		currentWord.Clear();
		currentComment.Clear();
		currentIpa.Clear();
	}
	else if (!graphicsState.WritingWord)
	{
		currentWord.Clear();
		currentComment.Clear();
		currentIpa.Clear();
	}
}

static string? GetFontName(PdfPage page, string? name)
{
	if (name is null)
		return null;

	if (!page.Resources.Elements.TryGetValue("/Font", out var fonts))
		return null;

	while (fonts is PdfReference reference)
		fonts = reference.Value;

	if (fonts is not PdfDictionary fontsDict)
		return null;

	if (!fontsDict.Elements.TryGetValue(name, out var font))
		return null;

	while (font is PdfReference reference)
		font = reference.Value;

	if (font is not PdfDictionary fontDict)
		return null;

	if (!fontDict.Elements.TryGetString("/BaseFont", out var baseFont))
		return null;

	return baseFont;
}

//Console.WriteLine("Fertig!");

//void WriteWord(StreamWriter writer, StringBuilder currentWord, StringBuilder currentNonWord)
//{
//	string? ipa = null;
//	if (currentNonWord.Length > 0)
//	{
//		var ipaMatch = ipaRegex.Match(currentNonWord.ToString().Replace("[\u000e]", "(\u000e)"));
//		if (ipaMatch.Success)
//		{
//			ipa = ipaMatch.Groups[1].Value;
//			currentNonWord.Clear();
//		}
//	}

//	var words = currentWord.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
//	foreach (var w in words)
//	{
//		var word = w;
//		while (word.EndsWith(':') || word.EndsWith(';') || word.EndsWith('Y'))
//			word = word[..^1];
//		word = word.Replace("\u0002", string.Empty);

//		if (word.StartsWith('-') || word.EndsWith('-'))
//			continue;

//		word = accentRegex.Replace(word, m =>
//		{
//			var letter = m.Groups[1].Value;
//			var accent = m.Groups[2].Value;

//			return accent switch
//			{
//				"`" => (letter + "\u0300").Normalize(),
//				"´" => (letter + "\u0301").Normalize(),
//				"^" => (letter + "\u0302").Normalize(),
//				"~" => (letter + "\u0303").Normalize(),
//				"˚" or "\u001e" => (letter + "\u030A").Normalize(),
//				_ => throw new ArgumentException("Unbekannter Akzent: " + accent)
//			};
//		});

//		writer.Write(word);

//		if (ipa is not null && !ipa.Contains("..."))
//		{
//			writer.Write('\t');
//			var converted = ipa;
//			foreach (var (key, value) in ipaConversion)
//				converted = converted.Replace(key, value);
//			/*if (!converted.All(c => ipaCharacters.Contains(c.ToString())))
//			{

//			}*/
//			writer.Write(converted);

//			ipa_count++;
//		}

//		writer.WriteLine();
//	}

//	currentWord.Clear();
//}