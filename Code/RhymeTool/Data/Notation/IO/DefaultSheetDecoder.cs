using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public class DefaultSheetDecoder(ISheetEditorFormatter? formatter = null) : SheetDecoderBase(formatter ?? DefaultReaderFormatter.Instance)
{
	private readonly List<SheetLine> lines = new();

	private List<ContentInLine>? lastAttachmentLine = null;
	private List<(Note? Tuning, Queue<TabLineElement> Elements)>? currentTab = null;

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

			//Wurde vorher schon eine Tabulaturzeile gelesen?
			if (currentTab is not null)
			{
				//Finalisiere die Tabulaturzeile
				var tabLine = CreateTabLine(currentTab);
				lines.Add(tabLine);
				currentTab = null;
			}

			//Füge eine Leerzeile hinzu
			lines.Add(new SheetEmptyLine());
			return;
		}

		//Ist die Zeile eine Tabulaturzeile?
		if (TryParseTabLine(line))
		{
			//Wurde vorher schon eine Akkordzeile gelesen?
			if (lastAttachmentLine != null)
			{
				//Die vorherige Zeile bleibt eine reine Akkordzeile
				var newChordLine = CreateChordLine(lastAttachmentLine);
				lines.Add(newChordLine);
				lastAttachmentLine = null;
			}
			return;
		}

		//Wurde vorher schon eine Tabulaturzeile gelesen?
		if (currentTab is not null)
		{
			//Finalisiere die Tabulaturzeile
			var tabLine = CreateTabLine(currentTab);
			lines.Add(tabLine);
			currentTab = null;
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

	private SheetTabLine CreateTabLine(IReadOnlyList<(Note? Tuning, Queue<TabLineElement> Elements)> lines)
	{
		//Erzeuge die Tabulaturzeile
		var tabLine = new SheetTabLine(lines.Select((l, i) => l.Tuning ?? i switch
		{
			0 => Note.E,
			1 => Note.A,
			2 => Note.D,
			3 => Note.G,
			4 => Note.B,
			_ => Note.E
		}));

		//Lies alle Zeilen gleichzeitig von links nach rechts
		var currentBarLength = 0;
		var currentIndex = 0;
		while (lines.All(l => l.Elements.Count > 0))
		{
			//Lies die jeweils ersten Elemente
			var elements = lines.Select(l => l.Elements.Dequeue()).ToArray();

			//Sind die Elemente unterschiedlich breit?
			var maxWidth = elements.Max(e => e.Width);
			var checkMaxWidthAgain = false;
			if (maxWidth > 1)
				maxWidth = ExtendToWidthWhileNecessary(lines, elements, maxWidth);

			//Enthält die nächste Spalte (nicht nur) Leerzeichen?
			while (lines.Count(l => l.Elements.TryPeek(out var next) && next.Type == TabLineElementType.Space) is int spaceLines
				&& spaceLines > 0 && spaceLines < lines.Count)
			{
				//Erweitere alle Elemente um die nächste Spalte
				var i = 0;
				maxWidth = 0;
				foreach (var line in lines)
				{
					if (line.Elements.TryDequeue(out var next))
						elements[i] = elements[i].Append(next);
					var width = elements[i].Width;
					if (width > maxWidth)
						maxWidth = width;
					i++;
				}

				maxWidth = ExtendToWidthWhileNecessary(lines, elements, maxWidth);
			}

			//Sind alle Elemente Leerzeichen?
			while (lines.Any(l => l.Elements.Count != 0) && elements.All(e => e.Type == TabLineElementType.Space))
			{
				//Erweitere sie alle um das jeweils nächste Element, bis wieder nur Leerzeichen gelesen wurden
				bool onlySpaces;
				do
				{
					onlySpaces = true;
					elements = elements
						.Zip(lines)
						.Select(p =>
						{
							if (!p.Second.Elements.TryDequeue(out var next))
								return p.First;

							//Taktstriche gehen auch, müssen aber dann wieder hinzugefügt werden, um den Takt zu zählen
							if (next.Type == TabLineElementType.BarLine)
								p.Second.Elements.Enqueue(next);
							else if (next.Type != TabLineElementType.Space)
								onlySpaces = false;

							return p.First.Append(next);
						})
						.ToArray();
					checkMaxWidthAgain = true;
				}
				while (!onlySpaces);
			}

			//Sind immer noch alle Elemente Leerzeichen (und damit die Queues leer)?
			if (elements.All(e => e.Type == TabLineElementType.Space))
				break;

			//Muss die Breite nochmal geprüft werden?
			if (checkMaxWidthAgain)
			{
				//Sind die Elemente unterschiedlich breit?
				maxWidth = ExtendToWidthWhileNecessary(lines, elements, elements.Max(e => e.Width));
			}

			//Ist ein Element ein Taktstrich?
			if (elements.Any(e => e.Type == TabLineElementType.BarLine))
			{
				//(Alle Elemente müssen Taktstriche sein)
				/*var errorIndex = Array.FindIndex(elements, e => (e.Type == TabLineElementType.BarLine) != (elements[0].Type == TabLineElementType.BarLine));
				if (errorIndex >= 0)
				{
					//Lies die gültigen und ungültigen Zeilen separat
					var validElements = lines.Take(errorIndex).ToList();
					var invalidElements = lines.Skip(errorIndex).ToList();
					return CreateTabLines(validElements).Concat(CreateTabLines(invalidElements));
				}*/

				//Lege die Taktlänge fest und beginne einen neuen Takt
				tabLine.BarLength = currentBarLength;
				currentBarLength = 0;
				continue;
			}

			//Erzeuge die Komponente
			if (elements.Any(e => e.Note?.IsEmpty == false))
			{
				var component = new SheetTabLine.Component(elements.Select(e => e.Note ?? TabNote.Empty));
				tabLine.Components[currentIndex] = component;
			}

			//Erhöhe den Index und die Taktlänge
			currentIndex++;
			currentBarLength++;
		}

		return tabLine;

		static int ExtendToWidthWhileNecessary(IReadOnlyList<(Note? Tuning, Queue<TabLineElement> Elements)> lines, TabLineElement[] elements, int maxWidth)
		{
			var checkMaxWidthAgain = ExtendToWidth(lines, elements, maxWidth);
			while (checkMaxWidthAgain)
			{
				maxWidth = elements.Max(e => e.Width);
				checkMaxWidthAgain = ExtendToWidth(lines, elements, maxWidth);
			}
			return maxWidth;
		}

		static bool ExtendToWidth(IReadOnlyList<(Note? Tuning, Queue<TabLineElement> Elements)> lines, TabLineElement[] elements, int maxWidth)
		{
			var extended = false;
			if (maxWidth > 1)
			{
				//Erweitere alle Elemente um die nächste Spalte
				var i = 0;
				foreach (var line in lines)
				{
					//Ist das Element zu schmal?
					var element = elements[i];
					while (element.Width < maxWidth)
					{
						//Hole das nächste Element
						if (!line.Elements.TryDequeue(out var nextElement))
							break;

						//Füge es hinzu
						element = element.Append(nextElement);
						extended = true;
					}
					elements[i++] = element;
				}
			}

			return extended;
		}
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

	private bool TryParseTabLine(ReadOnlySpan<char> line)
	{
		//Beginnt die Zeile mit dem Tuning?
		var length = Formatter.TryReadNote(line, out var tuning);

		//Lies ggf. Leerzeichen und dann einen Taktstrich
		var offset = length;
		while (offset < line.Length && line[offset] == ' ')
			offset++;
		if (offset >= line.Length || line[offset] != '|')
			return false;

		//Gibt es schon eine aktuelle Tabulaturzeile?
		var elements = new Queue<TabLineElement>();
		if (currentTab is null)
			currentTab = new();

		//Füge die Liste und das Tuning hinzu
		currentTab.Add((length <= 0 ? null : tuning, elements));

		//Lese Elemente
		for (offset++; offset < line.Length; offset++)
		{
			switch (line[offset])
			{
				case ' ':
					elements.Enqueue(TabLineElement.Space);
					continue;
				case '|':
					elements.Enqueue(TabLineElement.BarLine);
					continue;
			}

			//Lies Note und Modifikatoren
			var noteLength = Formatter.TryReadTabNote(line[offset..], out var note, 1);
			if (noteLength <= 0)
			{
				//Keine TabLine
				currentTab.RemoveAt(currentTab.Count - 1);
				if (currentTab.Count == 0)
					currentTab = null;
				return false;
			}

			//Füge Note hinzu
			elements.Enqueue(new(TabLineElementType.Note, noteLength, note));
			offset += noteLength - 1;
		}

		//Erfolgreich gelesen
		return true;
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

			//Stehen zwei Wörter/Akkorde direkt hintereinander?
			if (contents.Count > 0)
			{
				var previousContent = contents[^1];
				if (previousContent.Content.Type is SheetVarietyLine.ContentType.Chord or SheetVarietyLine.ContentType.Word
					&& content.Value.Type is SheetVarietyLine.ContentType.Chord or SheetVarietyLine.ContentType.Word)
				{
					//Füge die Akkorde als Text zusammen und ersetze die vorherige Komponente
					var combinedText = new string(line[previousContent.Offset.Value..(offset + contentLength)]);
					contents[^1] = new(new SheetVarietyLine.ComponentContent(combinedText), combinedText, previousContent.Offset, previousContent.Length + new ContentOffset(contentLength));
					offset += contentLength;
					continue;
				}
			}

			//Merke Content und Offset
			contents.Add(new(content.Value, new string(line[offset..(offset + contentLength)]), new(offset), new(contentLength)));
			offset += contentLength;
		}

		//Berechne das Verhältnis Akkorde/Wortlänge
		var wordLength = 0;
		var chordLength = 0;
		var weight = 0;
		for (var i = contents.Count - 1; i >= 0; i--)
		{
			var content = contents[i];
			switch (content.Content.Type)
			{
				case SheetVarietyLine.ContentType.Word:
					wordLength += weight * content.Length.Value;
					break;
				case SheetVarietyLine.ContentType.Chord:
					weight++;
					chordLength += weight * content.Length.Value;
					break;
				default:
					continue;
			}
		}
		canBeAttachmentLine = chordLength > 0 && wordLength / chordLength <= 1;
		canBeChordOnlyLine = false;

		//Kann die Zeile eine reine Akkordzeile sein?
		/*var hasChords = contents.Any(c => c.Content.Type == SheetVarietyLine.ContentType.Chord);
		var hasWords = contents.Any(c => c.Content.Type == SheetVarietyLine.ContentType.Word);
		canBeAttachmentLine = hasChords && !hasWords;
		canBeChordOnlyLine = false;*/
		if (canBeAttachmentLine)
			//Sind alle Leerkomponenten maximal drei Zeichen lang?
			if (contents.All(c => c.Content.Type != SheetVarietyLine.ContentType.Space || c.Length.Value <= 3))
				canBeChordOnlyLine = true;

		return contents;
	}

	public override IEnumerable<SheetLine> Finalize()
	{
		//Gibt es noch eine Tabulaturzeile am Ende?
		if (currentTab is not null)
		{
			//Finalisiere die Tabulaturzeile
			var tabLine = CreateTabLine(currentTab);
			lines.Add(tabLine);
			currentTab = null;
		}

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

	private enum TabLineElementType
	{
		Space,
		BarLine,
		Note,
	}

	private readonly record struct TabLineElement(TabLineElementType Type, int Width, TabNote? Note)
	{
		public static TabLineElement Space { get; } = new(TabLineElementType.Space, 1, null);
		public static TabLineElement Empty { get; } = new(TabLineElementType.Note, 1, TabNote.Empty);
		public static TabLineElement BarLine { get; } = new(TabLineElementType.BarLine, 1, null);

		public TabLineElement Append(TabLineElement next)
		{
			if (Type == TabLineElementType.Space)
				return next with { Width = Width + next.Width };

			if (next.Type == TabLineElementType.Space)
				return this with { Width = Width + next.Width };

			if (Note?.IsEmpty == true)
				return next with { Width = Width + next.Width };

			if (next.Note?.IsEmpty == true)
				return this with { Width = Width + next.Width };

			if (Note is not null && next.Note is not null)
				return new TabLineElement(
					TabLineElementType.Note,
					Width + next.Width,
					new TabNote(((Note.Value.Value * 10) ?? 0) + (next.Note.Value.Value ?? 0), Note.Value.Modifier | next.Note.Value.Modifier));

			return this with { Width = Width + next.Width };
		}
	}

	public record DefaultReaderFormatter : DefaultSheetFormatter
	{
		public static new DefaultReaderFormatter Instance { get; } = new();
	}
}
