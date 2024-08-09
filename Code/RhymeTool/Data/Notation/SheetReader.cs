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

using ContentInLine = (
	Skinnix.RhymeTool.Data.Notation.SheetVarietyLine.ComponentContent Content,
	string OriginalContent,
	Skinnix.RhymeTool.Data.Notation.ContentOffset Offset,
	Skinnix.RhymeTool.Data.Notation.ContentOffset Length);

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetReader
{
    private static readonly string[] allowedChordSuffixes = [",", " ,", "->", " ->"];

    private readonly TextReader reader;
    private readonly List<SheetLine> lines = new();

    private List<ContentInLine>? lastAttachmentLine = null;

	public ISheetEditorFormatter? Formatter { get; set; } = DefaultSheetFormatter.Instance;

	private SheetReader(TextReader reader)
    {
        this.reader = reader;
    }

    public static SheetDocument ReadSheet(TextReader reader)
        => new SheetReader(reader).ReadSheet();

	public static Task<SheetDocument> ReadSheetAsync(TextReader reader, CancellationToken cancellation = default)
		=> new SheetReader(reader).ReadSheetAsync(cancellation);

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
		if (lastAttachmentLine != null)
		{
			//Die letzte Zeile bleibt eine reine Akkordzeile
			var newChordLine = CreateChordLine(lastAttachmentLine);
			lines.Add(newChordLine);
		}

		//Finalisiere das Dokument
		FinalizeLines();

        //Erzeuge das Dokument
        return new SheetDocument(lines);
    }

	private async Task<SheetDocument> ReadSheetAsync(CancellationToken cancellation = default)
	{
		//Ignoriere Leerzeilen am Anfang des Dokuments
		cancellation.ThrowIfCancellationRequested();
		string? line = await reader.ReadLineAsync(cancellation);
		while (line != null && string.IsNullOrWhiteSpace(line))
			line = await reader.ReadLineAsync(cancellation);

		//Lese das Dokument zeilenweise
		for (; line != null; line = await reader.ReadLineAsync(cancellation))
		{
			cancellation.ThrowIfCancellationRequested();

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
		if (lastAttachmentLine != null)
		{
			//Die letzte Zeile bleibt eine reine Akkordzeile
			cancellation.ThrowIfCancellationRequested();
			var newChordLine = CreateChordLine(lastAttachmentLine);
			lines.Add(newChordLine);
		}

		//Finalisiere das Dokument
		cancellation.ThrowIfCancellationRequested();
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
            if (lastAttachmentLine != null)
            {
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = CreateChordLine(lastAttachmentLine);
                lines.Add(newChordLine);
                lastAttachmentLine = null;
            }

            //Füge eine Leerzeile hinzu
            lines.Add(new SheetEmptyLine());
            return;
        }

		//Lese die Zeile
		var contentLine = TryParseLine(line, out var canBeChordOnlyLine, out var canBeAttachmentLine);

		//Kann die Zeile eine reine Akkordzeile sein?
		if (canBeChordOnlyLine)
		{
			//Wurde vorher schon eine Akkordzeile gelesen?
			if (lastAttachmentLine != null)
			{
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = CreateChordLine(lastAttachmentLine);
				lastAttachmentLine = null;
				lines.Add(newChordLine);
			}

			//Diese Zeile wird auch eine reine Akkordzeile
			var chordLine = CreateChordLine(contentLine);
			lines.Add(chordLine);
			return;
		}

		//Kann die Zeile eine Attachmentzeile sein?
		if (canBeAttachmentLine)
		{
			//Wurde vorher schon eine Attachmentzeile gelesen?
			if (lastAttachmentLine != null)
			{
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = CreateChordLine(lastAttachmentLine);
				lines.Add(newChordLine);
			}

			//Merke die Akkordzeile für ggf. nächste Textzeilen
			lastAttachmentLine = contentLine;
			return;
		}

		//Erzeuge eine Textzeile
		var textLine = CreateTextLine(contentLine, lastAttachmentLine);
		lines.Add(textLine);
		lastAttachmentLine = null;
    }

	private SheetVarietyLine CreateChordLine(IEnumerable<ContentInLine> chords)
	{
		var components = new List<SheetVarietyLine.Component>();
		foreach (var chord in chords)
		{
			//Füge den Inhalt hinzu
			var component = new SheetVarietyLine.VarietyComponent(chord.Content);
			components.Add(component);
		}

		return new(components);
	}

	private SheetVarietyLine CreateTextLine(IReadOnlyList<ContentInLine> contents, IReadOnlyList<ContentInLine>? attachments)
	{
		var components = new List<SheetVarietyLine.VarietyComponent>(contents.Count);
		var attachmentIndex = 0;
		foreach (var content in contents)
		{
			//Wandle ggf. den Content um
			var componentContent = content.Content;
			if (componentContent.ContentType == SheetVarietyLine.ContentType.Chord)
				componentContent = new SheetVarietyLine.ComponentContent(content.OriginalContent);

			//Erzeuge die Komponente
			var component = new SheetVarietyLine.VarietyComponent(componentContent);

			//Finde ggf. Attachments
			if (attachments is not null)
			{
				var contentEnd = content.Offset + content.Length;
				for (; attachmentIndex < attachments.Count; attachmentIndex++)
				{
					//Nur Leerzeichen?
					if (attachments[attachmentIndex].Content.ContentType == SheetVarietyLine.ContentType.Space)
						continue;

					//Ist das Attachment zu weit hinten?
					var attachment = attachments[attachmentIndex];
					if (attachment.Offset >= contentEnd)
						break;

					//Füge das Attachment hinzu
					component.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(attachment.Offset - content.Offset, attachment.Content));
				}
			}

			//Versuche die Komponente an die vorherige anzufügen, ansonsten füge sie als eigene Komponente hinzu
			if (components.Count == 0 || components[^1].TryMerge(component, ContentOffset.MaxValue, Formatter) is null)
				components.Add(component);
		}

		//Sind noch Attachments übrig?
		if (attachments is not null && attachmentIndex < attachments.Count)
		{
			//Füge Leerzeichen hinzu, bis die Attachments passen
			var lineLength = contents[^1].Offset + contents[^1].Length;
			var spaceNeeded = attachments[^1].Offset - lineLength + ContentOffset.One;
			var spaceComponent = SheetVarietyLine.VarietyComponent.CreateSpace(spaceNeeded, Formatter);

			//Füge die Attachments dem Leerzeichen hinzu
			for (; attachmentIndex < attachments.Count; attachmentIndex++)
			{
				var chord = attachments[attachmentIndex];
				spaceComponent.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(chord.Offset - lineLength, chord.Content));
			}

			//Füge ggf. das Leerzeichen an die letzte Komponente an
			var lastComponent = components[^1];
			if (lastComponent.TryMerge(spaceComponent, ContentOffset.MaxValue, Formatter) is null)
			{
				//Füge das Leerzeichen als eigene Komponente hinzu
				components.Add(spaceComponent);
			}
		}

		//Erzeuge die Zeile
		return new SheetVarietyLine(components);
	}

	private List<ContentInLine> TryParseLine(ReadOnlySpan<char> line, out bool canBeChordOnlyLine, out bool canBeAttachmentLine)
	{
		var contents = new List<ContentInLine>();
		for (var offset = 0; ;)
		{
			//Lese nächsten Content
			var contentLength = SheetVarietyLine.ComponentContent.TryRead(line[offset..], out var content, Formatter);
			if (contentLength <= 0 || content is null)
				break;

			//Stehen zwei Akkorde direkt hintereinander?
			if (contents.Count > 0)
			{
				var previousContent = contents[^1];
				if (previousContent.Content.ContentType == SheetVarietyLine.ContentType.Chord && content.Value.ContentType == SheetVarietyLine.ContentType.Chord)
				{
					//Füge die Akkorde als Text zusammen und ersetze die vorherige Komponente
					var combinedText = new string(line[previousContent.Offset.Value..(offset + contentLength)]);
					contents[^1] = (new SheetVarietyLine.ComponentContent(combinedText), combinedText, previousContent.Offset, previousContent.Length + new ContentOffset(contentLength));
				}
			}

			//Merke Content und Offset
			contents.Add((content.Value, new string(line[offset..(offset + contentLength)]), new(offset), new(contentLength)));
			offset += contentLength;
		}

		//Kann die Zeile eine reine Akkordzeile sein?
		var hasChords = contents.Any(c => c.Content.ContentType == SheetVarietyLine.ContentType.Chord);
		var hasWords = contents.Any(c => c.Content.ContentType == SheetVarietyLine.ContentType.Word);
		canBeAttachmentLine = hasChords && !hasWords;
		canBeChordOnlyLine = false;
		if (canBeAttachmentLine)
		{
			//Sind alle Leerkomponenten nur ein Zeichen lang?
			if (contents.All(c => c.Content.ContentType != SheetVarietyLine.ContentType.Space || c.Length == ContentOffset.One))
				canBeChordOnlyLine = true;
		}

		return contents;
	}

	//private SheetVarietyLine CreateChordLine(IEnumerable<ChordInLine> chords)
	//{
	//	var components = new List<SheetVarietyLine.Component>();
	//	ContentOffset offset = ContentOffset.Zero;
	//	foreach (var chord in chords)
	//	{
	//		if (chord.Offset > offset)
	//		{
	//			//Füge Leerzeichen hinzu
	//			var spaceLength = chord.Offset - offset;
	//			components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(spaceLength, Formatter));
	//			offset += spaceLength;
	//		}

	//		//Füge den Akkord hinzu
	//		if (chord.Chord is not null)
	//			components.Add(new SheetVarietyLine.VarietyComponent(chord.Chord));
	//		if (chord.Suffix is not null)
	//		{
	//			var suffixDifference = chord.SuffixOffset - chord.Length - chord.Offset;
	//			if (suffixDifference > ContentOffset.Zero)
	//				components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(suffixDifference, Formatter));

	//			components.Add(new SheetVarietyLine.VarietyComponent(chord.Suffix));
	//		}

	//		offset += chord.Length + chord.SuffixOffset + new ContentOffset(chord.Suffix?.Length ?? 0);
	//	}

	//	return new(components);
	//}

 // //  private void CloseCurrentSegment()
 // //  {
 // //      //Gibt es noch eine gemerkte Akkordzeile am Ende?
 // //      if (lastChordLine != null)
 // //      {
	//	//	//Die letzte Zeile bleibt eine reine Akkordzeile
	//	//	var newChordLine = CreateChordLine(lastChordLine);
	//	//	lines.Add(newChordLine);
 // //      }

 // //      //Entferne Leerzeilen am Anfang und Ende
 // //      while (lines.Count > 0 && lines[0] is SheetEmptyLine)
 // //          lines.RemoveAt(0);
 // //      while (lines.Count > 0 && lines[^1] is SheetEmptyLine)
 // //          lines.RemoveAt(lines.Count - 1);

	//	////Füge das Segment hinzu
	//	//var newSegment = new SheetSegment(lines);
	//	//if (currentSegmentTitle)
	//	//newSegment.TitleLine = currentSegmentTitle ?? string.Empty;
	//	//segments.Add(newSegment);
 // //  }

 //   private string? TryParseSegmentTitle(string line)
 //   {
 //       var trimmed = line.Trim();
 //       if (trimmed.Length <= 2) return null;

 //       if (trimmed[0] == '[' && trimmed[^1] == ']')
 //           return trimmed[1..^1];

 //       return null;
 //   }

	//private List<ChordInLine>? TryParseChordLine(ReadOnlySpan<char> line)
 //   {
 //       var chords = new List<ChordInLine>();
 //       var offset = 0;
 //       while (offset < line.Length)
 //       {
 //           //Überspringe Leerzeichen
 //           while (offset < line.Length && char.IsWhiteSpace(line[offset]))
 //               offset++;

	//		//Ist das Ende erreicht?
	//		if (offset >= line.Length)
	//			break;

 //           //Lese Akkord
 //           var chordLength = Chord.TryRead(line[offset..], out var chord);
 //           if (chordLength == -1 || chord == null)
 //               return null;

 //           //Setze Offset hinter den Akkord
 //           var chordOffset = offset;
 //           offset += chordLength;

 //           //Lese Suffix
 //           var suffixOffset = offset;
 //           bool suffixFound;
 //           do
 //           {
 //               suffixFound = false;
 //               foreach (var allowedString in allowedChordSuffixes)
 //               {
 //                   if (line[offset..].StartsWith(allowedString))
 //                   {
 //                       suffixFound = true;
 //                       offset += allowedString.Length;
 //                       break;
 //                   }
 //               }
 //           }
 //           while (suffixFound && offset < line.Length);

 //           //Suffix gefunden?
 //           string? suffix = null;
 //           if (offset > suffixOffset)
 //           {
 //               suffix = new string(line[suffixOffset..offset]);
 //           }

 //           //Merke Akkord und Offset
 //           chords.Add((chord, new(chordLength), suffix, new(chordOffset), new(suffixOffset)));
 //       }

 //       return chords;
 //   }

 //   private List<SheetVarietyLine.Component> ParseTextLine(ReadOnlySpan<char> line, IEnumerable<ChordInLine>? chordLine)
 //   {
 //       var components = new List<SheetVarietyLine.Component>();
 //       var offset = 0;
 //       var chordEnumerator = (chordLine ?? Enumerable.Empty<ChordInLine>()).GetEnumerator();
 //       ChordInLine nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
 //       var nextChordOffset = nextChord.Chord != null ? nextChord.Offset : ContentOffset.MaxValue;
 //       while (offset < line.Length)
 //       {
 //           //Whitespaces oder Zeichen?
 //           var isWordWhitespace = char.IsWhiteSpace(line[offset]);

	//		//Lese bis zum nächsten Whitespace-Wechsel
	//		var startOffset = offset;
	//		for (; offset < line.Length && char.IsWhiteSpace(line[offset]) == isWordWhitespace; offset++) ;

	//		//Finde alle Attachments, die in diesem Bereich liegen
	//		var attachments = new List<SheetVarietyLine.VarietyComponent.Attachment>();
	//		while (nextChord.Length != ContentOffset.Zero && nextChord.Offset.Value < offset)
	//		{
	//			//Füge den Akkord hinzu
	//			if (nextChord.Chord is not null)
	//				attachments.Add(new SheetVarietyLine.VarietyComponent.VarietyAttachment(nextChord.Offset - new ContentOffset(startOffset), nextChord.Chord));

	//			//Füge ggf. Text hinzu
	//			//if (nextChord.Suffix is not null)
	//			//	attachments.Add(new SheetVarietyLine.VarietyComponent.VarietyAttachment(nextChord.SuffixOffset - new ContentOffset(startOffset) + nextChord.SuffixOffset, nextChord.Suffix));

	//			//Wechle zum nächsten Akkord
	//			nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
	//			nextChordOffset = nextChord.Chord != null ? nextChord.Offset : ContentOffset.MaxValue;
	//		}

	//		//Erzeuge den String
	//		SheetVarietyLine.VarietyComponent component;
	//		if (!isWordWhitespace)
	//		{
	//			component = SheetVarietyLine.VarietyComponent.FromString(new string(line[startOffset..offset]), Formatter);
	//		}
	//		else
	//		{
	//			var spaceLength = 0;
	//			foreach (var c in line[startOffset..offset])
	//			{
	//				//Tabulatoren zählen als 4 Leerzeichen, alles andere als 1
	//				if (c == '\t')
	//					spaceLength += 4;
	//				else
	//					spaceLength++;
	//			}
	//			component = SheetVarietyLine.VarietyComponent.CreateSpace(new(spaceLength), Formatter);
	//		}

	//		//Füge die Attachments hinzu
	//		component.AddAttachments(attachments);

	//		//Füge die Komponente hinzu
	//		components.Add(component);
	//	}

	//	//Verlänge ggf. die Zeile, um die nächsten Akkorde zu erfassen
	//	while (nextChord.Chord != null)
 //       {
 //           //Verlängere die Zeile um Leerzeichen
 //           if (nextChordOffset.Value > offset)
 //           {
 //               components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(nextChordOffset - new ContentOffset(offset), Formatter));
 //               offset = nextChordOffset.Value;
 //           }

 //           //Füge ein einzelnes Leerzeichen als Wort hinzu
	//		var word = new SheetVarietyLine.VarietyComponent(" ");
 //           offset++;

	//		//Füge den Akkord und ggf. Suffix hinzu
	//		if (nextChord.Chord is not null)
	//			word.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(ContentOffset.Zero, nextChord.Chord));
	//		if (nextChord.Suffix is not null)
	//			word.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(nextChord.SuffixOffset, nextChord.Suffix));

	//		//Füge das Wort hinzu
	//		components.Add(word);

 //           //Wechle zum nächsten Akkord
 //           nextChord = chordEnumerator.MoveNext() ? chordEnumerator.Current : default;
 //           nextChordOffset = nextChord.Chord != null ? nextChord.Offset : ContentOffset.MaxValue;
 //       }

 //       return components;
 //   }

	private void FinalizeLines()
	{
		////Entferne Leerzeilen am Anfang und Ende
		//while (lines.Count > 0 && lines[0] is SheetEmptyLine)
		//	lines.RemoveAt(0);
		//while (lines.Count > 0 && lines[^1] is SheetEmptyLine)
		//	lines.RemoveAt(lines.Count - 1);
	}
}
