namespace Skinnix.RhymeTool.Data.Notation.IO;

public class DefaultSheetDecoder(ISheetEditorFormatter? formatter = null) : SheetDecoderBase(formatter ?? DefaultReaderFormatter.Instance)
{
	private readonly List<SheetLine> lines = new();

	private List<ContentInLine>? lastAttachmentLine = null;
	private SheetTabLine? currentTab = null;

	public DefaultSheetDecoder() : this(null) { }

	public override void ProcessLine(string line)
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
			if (componentContent.Type == SheetVarietyLine.ContentType.Chord)
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
					if (attachments[attachmentIndex].Content.Type == SheetVarietyLine.ContentType.Space)
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
				//Füge das Leerzeichen als eigene Komponente hinzu
				components.Add(spaceComponent);
		}

		//Erzeuge die Zeile
		return new SheetVarietyLine(components);
	}

	//private SheetTabLine? TryParseTabLine(ReadOnlySpan<char> line)
	//{
	//	//Beginnt die Zeile mit dem Tuning?
	//	var length = Formatter.TryReadNote(line, out var tuning);

	//	//Lies ggf. einen Taktstrich
	//	var offset = length;
	//	if (line[offset] != '|')
	//		return null;

	//	//Gibt es schon eine aktuelle Tabulaturzeile?
	//	SheetTabLine.TabLineDefinition lineDefinition;
	//	if (currentTab is not null)
	//	{
	//		if (length < 0)
	//		{
	//			//Rate Tuning
	//			tuning = currentTab.Lines.Count switch
	//			{
	//				1 => Note.A,
	//				2 => Note.D,
	//				3 => Note.G,
	//				4 => Note.B,
	//				5 => Note.E,
	//				_ => Note.E,
	//			};
	//		}

	//		//Füge Zeile hinzu
	//		lineDefinition = currentTab.Lines.Add(tuning);
	//	}
	//	else
	//	{
	//		//Standardtuning
	//		if (length < 0)
	//			tuning = Note.E;

	//		//Erzeuge neue Tabulaturzeile
	//		currentTab = new SheetTabLine([tuning]);
	//		lineDefinition = currentTab.Lines[0];
	//	}

	//	//Füge Noten hinzu
	//	var firstBarLine = true;
	//	for (offset++; offset < line.Length; offset++)
	//	{
	//		//Lies Note und Modifikatoren
	//		var noteLength = Formatter.TryReadNoteName(line[offset..], out var note);
	//		if (noteLength <= 0)
	//			break;

	//		//Füge Note hinzu
	//		lineDefinition.Add(note);
	//		offset += noteLength - 1;
	//	}
	//}

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
				if (previousContent.Content.Type == SheetVarietyLine.ContentType.Chord && content.Value.Type == SheetVarietyLine.ContentType.Chord)
				{
					//Füge die Akkorde als Text zusammen und ersetze die vorherige Komponente
					var combinedText = new string(line[previousContent.Offset.Value..(offset + contentLength)]);
					contents[^1] = new(new SheetVarietyLine.ComponentContent(combinedText), combinedText, previousContent.Offset, previousContent.Length + new ContentOffset(contentLength));
				}
			}

			//Merke Content und Offset
			contents.Add(new(content.Value, new string(line[offset..(offset + contentLength)]), new(offset), new(contentLength)));
			offset += contentLength;
		}

		//Kann die Zeile eine reine Akkordzeile sein?
		var hasChords = contents.Any(c => c.Content.Type == SheetVarietyLine.ContentType.Chord);
		var hasWords = contents.Any(c => c.Content.Type == SheetVarietyLine.ContentType.Word);
		canBeAttachmentLine = hasChords && !hasWords;
		canBeChordOnlyLine = false;
		if (canBeAttachmentLine)
			//Sind alle Leerkomponenten nur ein Zeichen lang?
			if (contents.All(c => c.Content.Type != SheetVarietyLine.ContentType.Space || c.Length == ContentOffset.One))
				canBeChordOnlyLine = true;

		return contents;
	}

	public override IEnumerable<SheetLine> Finalize()
	{
		//Gibt es noch eine gemerkte Akkordzeile am Ende?
		if (lastAttachmentLine != null)
		{
			//Die letzte Zeile bleibt eine reine Akkordzeile
			var newChordLine = CreateChordLine(lastAttachmentLine);
			lines.Add(newChordLine);
		}

		//Gebe die Zeilen zurück
		return lines;
	}

	private readonly record struct ContentInLine(SheetVarietyLine.ComponentContent Content, string OriginalContent, ContentOffset Offset, ContentOffset Length);

	public record DefaultReaderFormatter : DefaultSheetFormatter
	{
		public static new DefaultReaderFormatter Instance { get; } = new();
	}
}
