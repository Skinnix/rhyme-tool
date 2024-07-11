using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation.Display;
using ChordInLine = (
	Skinnix.RhymeTool.Data.Notation.Chord Chord,
	Skinnix.RhymeTool.Data.Notation.ContentOffset Length,
	string? Suffix,
	Skinnix.RhymeTool.Data.Notation.ContentOffset Offset,
	Skinnix.RhymeTool.Data.Notation.ContentOffset SuffixOffset);

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetReader
{
    private static readonly string[] allowedChordSuffixes = [",", " ,", "->", " ->"];

    private readonly TextReader reader;
    private readonly List<SheetLine> lines = new();

    private List<ChordInLine>? lastChordLine = null;

	public ISheetEditorFormatter? Formatter { get; set; }

    private SheetReader(TextReader reader)
    {
        this.reader = reader;
    }

    public static SheetDocument ReadSheet(TextReader reader)
        => new SheetReader(reader).ReadSheet();

	public static Task<SheetDocument> ReadSheetAsync(TextReader reader)
		=> new SheetReader(reader).ReadSheetAsync();

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

		//Gibt es noch eine gemerkte Akkordzeile am Ende?
		if (lastChordLine != null)
		{
			//Die letzte Zeile bleibt eine reine Akkordzeile
			var newChordLine = CreateChordLine(lastChordLine);
			lines.Add(newChordLine);
		}

		//Finalisiere das Dokument
		FinalizeLines();

        //Erzeuge das Dokument
        return new SheetDocument(lines);
    }

	private async Task<SheetDocument> ReadSheetAsync()
	{
		//Ignoriere Leerzeilen am Anfang des Dokuments
		string? line = await reader.ReadLineAsync();
		while (line != null && string.IsNullOrWhiteSpace(line))
			line = await reader.ReadLineAsync();

		//Lese das Dokument zeilenweise
		for (; line != null; line = await reader.ReadLineAsync())
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

		//Gibt es noch eine gemerkte Akkordzeile am Ende?
		if (lastChordLine != null)
		{
			//Die letzte Zeile bleibt eine reine Akkordzeile
			var newChordLine = CreateChordLine(lastChordLine);
			lines.Add(newChordLine);
		}

		//Finalisiere das Dokument
		FinalizeLines();

		//Erzeuge das Dokument
		return new SheetDocument(lines);
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
				var newChordLine = CreateChordLine(lastChordLine);
                lines.Add(newChordLine);
                lastChordLine = null;
            }

            //Füge eine Leerzeile hinzu
            lines.Add(new SheetEmptyLine());
            return;
        }

    //    //Versuche die Zeile als Überschrift zu lesen
    //    var segmentTitle = TryParseSegmentTitle(line);
    //    if (segmentTitle != null)
    //    {
    //        //Wurde vorher schon eine Akkordzeile gelesen?
    //        if (lastChordLine != null)
    //        {
				////Die vorherige Zeile bleibt eine reine Akkordzeile
				//var newChordLine = CreateChordLine(lastChordLine);
				//lines.Add(newChordLine);
    //            lastChordLine = null;
    //        }

    //        //Erstes Segment?
    //        if (segments.Count == 0 && currentSegmentTitle == null && lines.Count == 0)
    //        {
    //            //Überschrift des ersten Segments
    //            currentSegmentTitle = segmentTitle;
    //            return;
    //        }

    //        //Schließe das bisherige Segment
    //        CloseCurrentSegment();

    //        //Merke die Überschrift für das nächste Segment
    //        currentSegmentTitle = segmentTitle;
    //        return;
    //    }

        //Versuche die Zeile als Akkordzeile zu lesen
        var chordLine = TryParseChordLine(line);
        if (chordLine != null)
        {
            //Wurde vorher schon eine Akkordzeile gelesen?
            if (lastChordLine != null)
            {
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = CreateChordLine(lastChordLine);
				lines.Add(newChordLine);
            }

            //Merke die Akkordzeile für ggf. nächste Textzeilen
            lastChordLine = chordLine;
            return;
        }

        //Trenne die Zeile in Wörter auf
        var textLineComponents = ParseTextLine(line, lastChordLine).ToList();
        lastChordLine = null;
		var textLine = new SheetVarietyLine(textLineComponents);
        lines.Add(textLine);
    }

	private SheetVarietyLine CreateChordLine(IEnumerable<ChordInLine> chords)
	{
		var components = new List<SheetVarietyLine.Component>();
		ContentOffset offset = ContentOffset.Zero;
		foreach (var chord in chords)
		{
			if (chord.Offset > offset)
			{
				//Füge Leerzeichen hinzu
				var spaceLength = chord.Offset - offset;
				components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(spaceLength, Formatter));
				offset += spaceLength;
			}

			//Füge den Akkord hinzu
			if (chord.Chord is not null)
				components.Add(new SheetVarietyLine.VarietyComponent(chord.Chord));
			if (chord.Suffix is not null)
			{
				if (chord.SuffixOffset > ContentOffset.Zero)
					components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(chord.SuffixOffset, Formatter));

				components.Add(new SheetVarietyLine.VarietyComponent(chord.Suffix));
			}

			offset += chord.Length + chord.SuffixOffset + new ContentOffset(chord.Suffix?.Length ?? 0);
		}

		return new(components);
	}

  //  private void CloseCurrentSegment()
  //  {
  //      //Gibt es noch eine gemerkte Akkordzeile am Ende?
  //      if (lastChordLine != null)
  //      {
		//	//Die letzte Zeile bleibt eine reine Akkordzeile
		//	var newChordLine = CreateChordLine(lastChordLine);
		//	lines.Add(newChordLine);
  //      }

  //      //Entferne Leerzeilen am Anfang und Ende
  //      while (lines.Count > 0 && lines[0] is SheetEmptyLine)
  //          lines.RemoveAt(0);
  //      while (lines.Count > 0 && lines[^1] is SheetEmptyLine)
  //          lines.RemoveAt(lines.Count - 1);

		////Füge das Segment hinzu
		//var newSegment = new SheetSegment(lines);
		//if (currentSegmentTitle)
		//newSegment.TitleLine = currentSegmentTitle ?? string.Empty;
		//segments.Add(newSegment);
  //  }

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

			//Ist das Ende erreicht?
			if (offset >= line.Length)
				break;

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
            chords.Add((chord, new(chordLength), suffix, new(chordOffset), new(suffixOffset)));
        }

        return chords;
    }

    private List<SheetVarietyLine.Component> ParseTextLine(ReadOnlySpan<char> line, IEnumerable<ChordInLine>? chordLine)
    {
        var components = new List<SheetVarietyLine.Component>();
        var offset = 0;
        var chordEnumerator = (chordLine ?? Enumerable.Empty<ChordInLine>()).GetEnumerator();
        ChordInLine nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
        var nextChordOffset = nextChord.Chord != null ? nextChord.Offset : ContentOffset.MaxValue;
        while (offset < line.Length)
        {
            //Whitespaces oder Zeichen?
            var isWordWhitespace = char.IsWhiteSpace(line[offset]);

			//Lese bis zum nächsten Whitespace-Wechsel
			var startOffset = offset;
			for (; offset < line.Length && char.IsWhiteSpace(line[offset]) == isWordWhitespace; offset++) ;

			//Finde alle Attachments, die in diesem Bereich liegen
			var attachments = new List<SheetVarietyLine.VarietyComponent.Attachment>();
			while (nextChord.Length != ContentOffset.Zero && nextChord.Offset.Value < offset)
			{
				//Füge den Akkord hinzu
				if (nextChord.Chord is not null)
					attachments.Add(new SheetVarietyLine.VarietyComponent.VarietyAttachment(nextChord.Offset - new ContentOffset(startOffset), nextChord.Chord));

				//Füge ggf. Text hinzu
				if (nextChord.Suffix is not null)
					attachments.Add(new SheetVarietyLine.VarietyComponent.VarietyAttachment(nextChord.SuffixOffset - new ContentOffset(startOffset) + nextChord.SuffixOffset, nextChord.Suffix));

				//Wechle zum nächsten Akkord
				nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
				nextChordOffset = nextChord.Chord != null ? nextChord.Offset : ContentOffset.MaxValue;
			}

			//Erzeuge den String
			SheetVarietyLine.VarietyComponent component;
			if (!isWordWhitespace)
			{
				component = SheetVarietyLine.VarietyComponent.FromString(new string(line[startOffset..offset]), Formatter, SheetVarietyLine.SpecialContentType.None);
			}
			else
			{
				var spaceLength = 0;
				foreach (var c in line[startOffset..offset])
				{
					//Tabulatoren zählen als 4 Leerzeichen, alles andere als 1
					if (c == '\t')
						spaceLength += 4;
					else
						spaceLength++;
				}
				component = SheetVarietyLine.VarietyComponent.CreateSpace(new(spaceLength), Formatter, SheetVarietyLine.SpecialContentType.None);
			}

			//Füge die Attachments hinzu
			component.AddAttachments(attachments);

			//Füge die Komponente hinzu
			components.Add(component);
		}

		//Verlänge ggf. die Zeile, um die nächsten Akkorde zu erfassen
		while (nextChord.Chord != null)
        {
            //Verlängere die Zeile um Leerzeichen
            if (nextChordOffset.Value > offset)
            {
                components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(nextChordOffset - new ContentOffset(offset), Formatter, SheetVarietyLine.SpecialContentType.None));
                offset = nextChordOffset.Value;
            }

            //Füge ein einzelnes Leerzeichen als Wort hinzu
			var word = new SheetVarietyLine.VarietyComponent(" ", SheetVarietyLine.SpecialContentType.None);
            offset++;

			//Füge den Akkord und ggf. Suffix hinzu
			if (nextChord.Chord is not null)
				word.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(ContentOffset.Zero, nextChord.Chord));
			if (nextChord.Suffix is not null)
				word.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(nextChord.SuffixOffset, nextChord.Suffix));

			//Füge das Wort hinzu
			components.Add(word);

            //Wechle zum nächsten Akkord
            nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
            nextChordOffset = nextChord.Chord != null ? nextChord.Offset : ContentOffset.MaxValue;
        }

        return components;
    }

	private void FinalizeLines()
	{
		////Entferne Leerzeilen am Anfang und Ende
		//while (lines.Count > 0 && lines[0] is SheetEmptyLine)
		//	lines.RemoveAt(0);
		//while (lines.Count > 0 && lines[^1] is SheetEmptyLine)
		//	lines.RemoveAt(lines.Count - 1);
	}
}
