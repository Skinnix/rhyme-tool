using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using ChordInLine = (Skinnix.RhymeTool.Data.Structure.Chord Chord, string? Suffix, int Offset, int SuffixOffset);

namespace Skinnix.RhymeTool.Data.Structure;

public class SheetReader
{
	private static readonly string[] allowedChordSuffixes = [",", " ,", "->", " ->"];

	private readonly TextReader reader;

	private List<SheetSegment> segments = new();
	private List<SheetLine> currentSegmentLines = new();
	private string? currentSegmentTitle = null;
	private List<ChordInLine>? lastChordLine = null;

	private SheetReader(TextReader reader)
	{
		this.reader = reader;
	}

	public static SheetDocument ReadSheet(TextReader reader)
		=> new SheetReader(reader).ReadSheet();

	private SheetDocument ReadSheet()
	{
		//Ignoriere Leerzeilen am Anfang des Dokuments
		string? line = reader.ReadLine();
		while (line != null && string.IsNullOrWhiteSpace(line))
			line = reader.ReadLine();

		//Lese das Dokument zeilenweise
		for (; line != null; line = reader.ReadLine())
		{
			//Schneide Zeilenumbrüche ab
			if (line.StartsWith("\r\n"))
				line = line[2..];
			else if (line.StartsWith("\n"))
				line = line[1..];
			else if (line.StartsWith("\r"))
				line = line[1..];

			//Lese die Zeile
			ReadLine(line);
		}

		//Schließe das letzte Segment
		CloseCurrentSegment();

		//Erzeuge das Dokument
		return new SheetDocument(segments);
	}

	private void ReadLine(string line)
	{
		//Ist die Zeile leer?
		if (string.IsNullOrWhiteSpace(line))
		{
			//Wurde vorher schon eine Akkordzeile gelesen?
			if (lastChordLine != null)
			{
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = new SheetChordLine();
				foreach (var chord in lastChordLine)
					newChordLine.Chords.Add(new PositionedChord(chord.Chord, chord.Offset)
					{
						Suffix = chord.Suffix
					});

				currentSegmentLines.Add(newChordLine);
				lastChordLine = null;
			}

			//Füge eine Leerzeile hinzu
			currentSegmentLines.Add(new SheetEmptyLine());
			return;
		}

		//Versuche die Zeile als Überschrift zu lesen
		var segmentTitle = TryParseSegmentTitle(line);
		if (segmentTitle != null)
		{
			//Wurde vorher schon eine Akkordzeile gelesen?
			if (lastChordLine != null)
			{
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = new SheetChordLine();
				foreach (var chord in lastChordLine)
					newChordLine.Chords.Add(new PositionedChord(chord.Chord, chord.Offset));

				currentSegmentLines.Add(newChordLine);
				lastChordLine = null;
			}

			//Erstes Segment?
			if (segments.Count == 0 && currentSegmentTitle == null && currentSegmentLines.Count == 0)
			{
				//Überschrift des ersten Segments
				currentSegmentTitle = segmentTitle;
				return;
			}

			//Schließe das bisherige Segment
			CloseCurrentSegment();

			//Merke die Überschrift für das nächste Segment
			currentSegmentTitle = segmentTitle;
			return;
		}

		//Versuche die Zeile als Akkordzeile zu lesen
		var chordLine = TryParseChordLine(line);
		if (chordLine != null)
		{
			//Wurde vorher schon eine Akkordzeile gelesen?
			if (lastChordLine != null)
			{
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = new SheetChordLine();
				foreach (var chord in lastChordLine)
				{
					newChordLine.Chords.Add(new PositionedChord(chord.Chord, chord.Offset)
					{
						Suffix = chord.Suffix
					});
				}

				currentSegmentLines.Add(newChordLine);
			}

			//Merke die Akkordzeile für ggf. nächste Textzeilen
			lastChordLine = chordLine;
			return;
		}

		//Trenne die Zeile in Wörter auf
		var textLineComponents = ParseTextLine(line, lastChordLine).ToList();
		lastChordLine = null;
		var textLine = new SheetChordedLine(textLineComponents);
		currentSegmentLines.Add(textLine);
	}

	private void CloseCurrentSegment()
	{
		//Gibt es noch eine gemerkte Akkordzeile am Ende?
		if (lastChordLine != null)
		{
			//Die letzte Zeile bleibt eine reine Akkordzeile
			var newChordLine = new SheetChordLine();
			foreach (var chord in lastChordLine)
				newChordLine.Chords.Add(new PositionedChord(chord.Chord, chord.Offset));

			currentSegmentLines.Add(newChordLine);
		}

		//Entferne Leerzeilen am Anfang und Ende
		while (currentSegmentLines.Count > 0 && currentSegmentLines[0] is SheetEmptyLine)
			currentSegmentLines.RemoveAt(0);
		while (currentSegmentLines.Count > 0 && currentSegmentLines[^1] is SheetEmptyLine)
			currentSegmentLines.RemoveAt(currentSegmentLines.Count - 1);

		//Füge das Segment hinzu
		segments.Add(new SheetSegment(currentSegmentLines)
		{
			Title = currentSegmentTitle
		});

		//Fange ein neues Segment an
		currentSegmentLines = new List<SheetLine>();
	}

	private string? TryParseSegmentTitle(string line)
	{
		var trimmed = line.Trim();
		if (trimmed.Length <= 2) return null;

		if (trimmed[0] == '[' && trimmed[^1] == ']')
			return trimmed[1..^1];

		return null;
	}

	private List<ChordInLine>? TryParseChordLine(ReadOnlySpan<char> line)
	{
		var chords = new List<ChordInLine>();
		var offset = 0;
		while (offset < line.Length)
		{
			//Überspringe Leerzeichen
			while (offset < line.Length && char.IsWhiteSpace(line[offset]))
				offset++;

			//Lese Akkord
			var chordLength = Chord.TryRead(line[offset..], out var chord);
			if (chordLength == -1 || chord == null)
				return null;

			//Setze Offset hinter den Akkord
			var chordOffset = offset;
			offset += chordLength;

			//Lese Suffix
			var suffixOffset = offset;
			bool suffixFound;
			do
			{
				suffixFound = false;
				foreach (var allowedString in allowedChordSuffixes)
				{
					if (line[offset..].StartsWith(allowedString))
					{
						suffixFound = true;
						offset += allowedString.Length;
						break;
					}
				}
			}
			while (suffixFound && offset < line.Length);

			//Suffix gefunden?
			string? suffix = null;
			if (offset > suffixOffset)
			{
				suffix = new string(line[suffixOffset..offset]);
			}

			//Merke Akkord und Offset
			chords.Add((chord, suffix, chordOffset, suffixOffset));
		}

		return chords;
	}

	private List<SheetChordedLineComponent> ParseTextLine(ReadOnlySpan<char> line, IEnumerable<ChordInLine>? chordLine)
	{
		var components = new List<SheetChordedLineComponent>();
		var offset = 0;
		var chordEnumerator = (chordLine ?? Enumerable.Empty<ChordInLine>()).GetEnumerator();
		ChordInLine nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
		var nextChordOffset = nextChord.Chord != null ? nextChord.Offset : int.MaxValue;
		while (offset < line.Length)
		{
			//Zähle Leerzeichen
			var spaceOffset = offset;
			while (spaceOffset < line.Length && spaceOffset < nextChordOffset && char.IsWhiteSpace(line[spaceOffset]))
			{
				spaceOffset++;
			}

			//Leerzeichen gefunden?
			if (spaceOffset > offset)
			{
				components.Add(new SheetChordedLineSpace(spaceOffset - offset));
				offset = spaceOffset;
			}

			//Ende der Zeile?
			if (offset >= line.Length)
				break;

			//Whitespaces oder Zeichen?
			var isWordWhitespace = char.IsWhiteSpace(line[offset]);

			//Lese eine Wortkomponente pro Akkord
			var lastComponentOffset = offset;
			var wordComponents = new List<WordComponent>();
			ChordInLine nextComponentChord = default;
			for (; offset < line.Length && (char.IsWhiteSpace(line[offset]) == isWordWhitespace); offset++)
			{
				if (offset >= nextChordOffset)
				{
					//Erzeuge ggf. eine Komponente für das bisher gelesene
					if (offset > lastComponentOffset)
					{
						var wordComponent = new WordComponent(new string(line[lastComponentOffset..offset]));
						wordComponents.Add(wordComponent);
						lastComponentOffset = offset;

						//Ordne ggf. den bisherigen Akkord zu
						if (nextComponentChord.Chord != null)
						{
							wordComponent.Attachments.Add(new WordComponentChord(nextComponentChord.Chord) { Offset = nextComponentChord.Offset - lastComponentOffset });
							if (nextComponentChord.Suffix != null)
								wordComponent.Attachments.Add(new WordComponentText(nextComponentChord.Suffix) { Offset = nextComponentChord.SuffixOffset - lastComponentOffset });
						}
					}

					//Ordne den Akkord der kommenden Wortkomponente zu
					nextComponentChord = nextChord;

					//Wechle zum nächsten Akkord
					nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
					nextChordOffset = nextChord.Chord != null ? nextChord.Offset : int.MaxValue;
				}
			}

			//Merke ggf. letzte Komponente
			if (offset > lastComponentOffset)
			{
				//Erzeuge eine Komponente für das bisher gelesene
				var wordComponent = new WordComponent(new string(line[lastComponentOffset..offset]));
				wordComponents.Add(wordComponent);

				//Ordne ggf. den bisherigen Akkord zu
				if (nextComponentChord.Chord != null)
				{
					wordComponent.Attachments.Add(new WordComponentChord(nextComponentChord.Chord) { Offset = nextComponentChord.Offset - lastComponentOffset });
					if (nextChord.Suffix != null)
						wordComponent.Attachments.Add(new WordComponentText(nextChord.Suffix) { Offset = nextChord.SuffixOffset - lastComponentOffset });
				}
			}

			//Speichere das Wort
			components.Add(new SheetChordedLineWord(wordComponents));
		}

		//Verlänge ggf. die Zeile, um die nächsten Akkorde zu erfassen
		while (nextChord.Chord != null)
		{
			//Verlängere die Zeile um Leerzeichen
			if (nextChordOffset > offset)
			{
				components.Add(new SheetChordedLineSpace(nextChordOffset - offset));
				offset = nextChordOffset;
			}

			//Füge ein einzelnes Leerzeichen als Wort hinzu
			var word = new WordComponent(" ");
			offset++;

			//Füge den Akkord und ggf. Suffix hinzu
			word.Attachments.Add(new WordComponentChord(nextChord.Chord));
			if (nextChord.Suffix != null)
				word.Attachments.Add(new WordComponentText(nextChord.Suffix) { Offset = nextChord.SuffixOffset - nextChord.Offset });
			components.Add(new SheetChordedLineWord(word));

			//Wechle zum nächsten Akkord
			nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
			nextChordOffset = nextChord.Chord != null ? nextChord.Offset : int.MaxValue;
		}

		return components;
	}
}
