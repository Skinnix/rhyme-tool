using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public static class SheetEncoderHelper
{
	public static void WriteTabLine(SheetTabLine line, ISheetBuilderFormatter formatter)
	{

	}
}

public static class SheetDecoderHelper
{
	public static (Note? Tuning, Queue<TabLineElement> Elements)? TryParseTabLine(ReadOnlySpan<char> line, ISheetEditorFormatter formatter)
	{
		//Beginnt die Zeile mit dem Tuning?
		var length = formatter.TryReadNote(line, out var tuning);

		//Lies ggf. Leerzeichen und dann einen Taktstrich
		var offset = length;
		while (offset < line.Length && line[offset] == ' ')
			offset++;
		if (offset >= line.Length)
			return null;
		if (line[offset] is not '|' or '-')
			offset--;

		//Gibt es schon eine aktuelle Tabulaturzeile?
		var elements = new Queue<TabLineElement>();

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
			var noteLength = formatter.TryReadTabNote(line[offset..], out var note, 1);
			if (noteLength <= 0)
				//Keine TabLine
				return null;

			//Füge Note hinzu
			elements.Enqueue(new(TabLineElementType.Note, noteLength, note));
			offset += noteLength - 1;
		}

		//Erfolgreich gelesen
		return (length <= 0 ? null : tuning, elements);
	}
	public static SheetTabLine CreateTabLine(IReadOnlyList<(Note? Tuning, Queue<TabLineElement> Elements)> lines)
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
		}))
		{
			BarLength = 0
		};

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
			if (lines.Any(l => l.Elements.Count != 0) && elements.All(e => e.Type == TabLineElementType.Space))
			{
				//Überspringe zuerst alle weiteren Leerzeichen
				TabLineElement?[] next;
				do
				{
					next = lines.Select(l => l.Elements.TryPeek(out var next) ? (TabLineElement?)next : null).ToArray();

					//Ist eine der Zeilen zu Ende?
					if (next.Contains(null))
						break;

					foreach (var line in lines)
						line.Elements.TryDequeue(out _);
				} while (next.All(n => n?.Type == TabLineElementType.Space));

				//Erweitere sie alle um das jeweils nächste Element, bis wieder nur Leerzeichen gelesen wurden
				//oder eine Zeile, die vorher ein Leerzeichen enthielt, jetzt wieder Inhalt hat
				var hadSpace = new bool[lines.Count];
				bool cancel = false;
				bool onlySpaces;
				while (true)
				{
					onlySpaces = true;

					//Lese die nächsten Zeilen
					next = lines.Select(l => l.Elements.TryPeek(out var next) ? (TabLineElement?)next : null).ToArray();

					//Ist eine der Zeilen zu Ende?
					if (next.Contains(null))
						break;

					//Ist eins der nächsten Elemente ein Taktstrich?
					if (next.Any(n => n?.Type == TabLineElementType.BarLine))
						break;

					//Sind alle nächsten Elemente Leerzeichen?
					if (next.All(n => n?.Type == TabLineElementType.Space))
						break;

					//Hatte eine der Zeilen schon Leerzeichen und enthält jetzt wieder Inhalt?
					foreach (var n in next.Index())
					{
						var space = hadSpace[n.Index] |= n.Item?.Type == TabLineElementType.Space;
						if (space && n.Item?.Type == TabLineElementType.Note)
							cancel = true;
					}
					if (cancel)
						break;

					//Hänge die nächsten Elemente an
					for (var i = 0; i < elements.Length; i++)
					{
						if (lines[i].Elements.TryDequeue(out var n))
							elements[i] = elements[i].Append(n);
					}


					//for (var i = 0; i < elements.Length; i++)
					//{
					//	//Ist die Zeile zu Ende?
					//	var line = lines[i];
					//	if (!line.Elements.TryDequeue(out var next))
					//	{
					//		cancel = true;
					//		continue;
					//	}

					//	//Ist das nächste Element ein Taktstrich?
					//	if (next.Type == TabLineElementType.BarLine)
					//	{
					//		//Füge den Taktstrich wieder hinzu, um den Takt korrekt zu beenden
					//		line.Elements.Enqueue(next);
					//		cancel = true;
					//		continue;
					//	}

					//	//Ist das nächste Element ein Leerzeichen?
					//	if (next.Type == TabLineElementType.Space)
					//	{
					//		//Wurde noch kein Inhalt gelesen?
					//		if (!hadContent[i])
					//		{

					//		}
					//		//Merke, dass die Zeile Leerzeichen enthielt
					//		hadSpace[i] = true;
					//		onlySpaces = false;
					//	}
					//	else
					//	{
					//		//Merke, dass die Zeile jetzt Inhalt enthält
					//		hadContent[i] = true;
					//		onlySpaces = false;
					//	}
					//}
					//elements = elements
					//	.Zip(lines)
					//	.Select((p, i) =>
					//	{
					//		if (!p.Second.Elements.TryDequeue(out var next))
					//			return p.First;

					//		//Taktstriche gehen auch, müssen aber dann wieder hinzugefügt werden, um den Takt zu zählen
					//		if (next.Type == TabLineElementType.BarLine)
					//			p.Second.Elements.Enqueue(next);
					//		else if (next.Type != TabLineElementType.Space)
					//			onlySpaces = false;

					//		return p.First.Append(next);
					//	})
					//	.ToArray();
					//checkMaxWidthAgain = true;
				}
				//while (!onlySpaces);
			}

			//Sind immer noch alle Elemente Leerzeichen (und damit die Queues leer)?
			if (elements.All(e => e.Type == TabLineElementType.Space))
				break;

			//Muss die Breite nochmal geprüft werden?
			if (checkMaxWidthAgain)
				//Sind die Elemente unterschiedlich breit?
				maxWidth = ExtendToWidthWhileNecessary(lines, elements, elements.Max(e => e.Width));

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

	public enum TabLineElementType
	{
		Space,
		BarLine,
		Note,
	}

	public readonly record struct TabLineElement(TabLineElementType Type, int Width, TabNote? Note)
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
					new TabNote((Note.Value.Value * 10 ?? 0) + (next.Note.Value.Value ?? 0), Note.Value.Modifier | next.Note.Value.Modifier));

			return this with { Width = Width + next.Width };
		}
	}
}
