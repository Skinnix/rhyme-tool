using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetVarietyLine : SheetLine, ISheetTitleLine
{
	private readonly List<Component> components;
	private readonly ContentEditing contentEditor;
	private readonly AttachmentEditing attachmentEditor;

	public ISheetDisplayLineEditing ContentEditor => contentEditor;
	public ISheetDisplayLineEditing AttachmentEditor => attachmentEditor;

	public int TextLineId => contentEditor.LineId;
	public int AttachmentLineId => attachmentEditor.LineId;

	public SheetVarietyLine()
	{
		components = new();

		contentEditor = new(this);
		attachmentEditor = new(this);
	}

	public SheetVarietyLine(IEnumerable<Component> components)
	{
		this.components = new(components);

		contentEditor = new(this);
		attachmentEditor = new(this);
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
	{
		//Erzeuge Builder
		var builders = new Component.LineBuilders(this,
			new SheetDisplayTextLine.Builder(),
			new SheetDisplayChordLine.Builder())
		{
			IsTitleLine = IsTitleLine(out _),
		};

		//Gehe durch alle Komponenten
		foreach (var component in components)
			component.BuildLines(builders, formatter);

		//Sind alle Zeilen leer?
		if (builders.CurrentLength == 0)
		{
			yield return new SheetDisplayEmptyLine(0) { Editing = contentEditor };
			yield break;
		}

		//Gib nichtleere Zeilen zurück
		if (builders.ChordLine.CurrentLength > 0)
		{
			//Strecke die Akkordzeile auf die Länge der Textzeile + 1
			builders.ChordLine.ExtendLength(builders.TextLine.CurrentLength + 1, 0);
			yield return builders.ChordLine.CreateDisplayLine(1, attachmentEditor);
		}
		if (builders.TextLine.CurrentLength > 0)
		{
			yield return builders.TextLine.CreateDisplayLine(0, contentEditor);
		}
	}

	#region Creation
	public static SheetVarietyLine CreateFrom(string text)
	{
		//Trenne den Text in Komponenten
		var words = text.SplitAlternating(char.IsWhiteSpace);

		//Erzeuge die Zeile
		var line = new SheetVarietyLine();
		foreach (var word in words)
			line.components.Add(VarietyComponent.FromString(word));

		return line;
	}
	#endregion

	#region Title
	public bool IsTitleLine(out string? title)
		=> IsTitleLine(out title, out _, out _);

	private bool IsTitleLine(out string? title, out IReadOnlyList<VarietyComponent> titleComponents, out int afterTitleIndex)
	{
		//Alles, was von Klammern umschlossen ist und keine Attachments hat, ist es ein Titel
		var titleBuilder = new StringBuilder();
		var titleComponentsList = new List<VarietyComponent>();
		titleComponents = titleComponentsList;
		var i = -1;
		foreach (var component in components)
		{
			i++;

			//Ist die Komponente keine VarietyComponent, kein Text oder hat Attachments?
			if (component is not VarietyComponent variety || variety.Content.Text is null || variety.Attachments.Count != 0)
			{
				title = null;
				afterTitleIndex = 0;
				return false;
			}

			//Erste Komponente muss mit öffnender Klammer beginnen
			if (i == 0 && !variety.Content.Text.StartsWith('['))
			{
				title = null;
				afterTitleIndex = 0;
				return false;
			}

			//Baue den Titel zusammen
			titleBuilder.Append(variety.Content.Text);
			titleComponentsList.Add(variety);
			if (variety.Content.Text.EndsWith(']'))
			{
				//Titel gefunden
				title = titleBuilder.ToString(1, titleBuilder.Length - 2);
				afterTitleIndex = i + 1;
				return true;
			}
		}

		title = null;
		afterTitleIndex = 0;
		return false;
	}
	#endregion

	#region Editing
	private class ContentEditing : ISheetDisplayLineEditing
	{
		public SheetVarietyLine Line { get; }
		SheetLine ISheetDisplayLineEditing.Line => Line;

		public int LineId => 0;

		public ContentEditing(SheetVarietyLine owner)
		{
			this.Line = owner;
		}

		public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetFormatter? formatter = null)
		{
			//Ist der Bereich leer?
			if (context.SelectionRange.Length == 0)
			{
				//Wird der Zeilenumbruch am Anfang entfernt?
				if (context.SelectionRange.Start == 0 && !forward)
				{
					//Gibt es eine Zeile davor?
					var lineBefore = context.GetLineBefore?.Invoke();
					if (lineBefore is null)
						return MetalineEditResult.Fail;

					//Ist die vorherige Zeile leer?
					if (lineBefore is SheetEmptyLine)
					{
						//Lösche die vorherige Zeile
						return new MetalineEditResult(true, new MetalineSelectionRange(this, SimpleRange.Zero))
						{
							RemoveLineBefore = true,
						};
					}

					//Ist die vorherige Zeile keine VarietyLine?
					if (lineBefore is not SheetVarietyLine varietyBefore)
						return MetalineEditResult.Fail;

					//Füge alle Komponenten dieser Zeile an das Ende der vorherigen Zeile an
					var lastComponent = varietyBefore.components.Count == 0 ? null : varietyBefore.components[^1];
					varietyBefore.components.AddRange(Line.components);

					//Prüfe, ob die Komponenten zusammengefügt werden können
					if (lastComponent is not null && Line.components.Count > 0)
					{
						if (lastComponent.TryMerge(Line.components[0], int.MaxValue, formatter))
						{
							//Entferne die zusammengefügte Komponente
							varietyBefore.components.Remove(Line.components[0]);
						}
					}

					return new MetalineEditResult(true, new MetalineSelectionRange(varietyBefore.ContentEditor, SimpleRange.CursorAt(lastComponent?.ContentRenderBounds.EndOffset ?? 0)))
					{
						RemoveLine = true,
					};
				}

				//Wird der Zeilenumbruch am Ende entfernt?
				if (forward && Line.components.Count == 0 || context.SelectionRange.End >= Line.components[^1].ContentRenderBounds.EndOffset)
				{
					//Gibt es eine Zeile danach?
					var lineAfter = context.GetLineAfter?.Invoke();
					if (lineAfter is null)
						return MetalineEditResult.Fail;

					//Ist die nächste Zeile leer?
					if (lineAfter is SheetEmptyLine)
					{
						//Lösche die nächste Zeile
						return new MetalineEditResult(true, new MetalineSelectionRange(this, SimpleRange.Zero))
						{
							RemoveLineAfter = true,
						};
					}

					//Ist die nächste Zeile keine VarietyLine?
					if (lineAfter is not SheetVarietyLine varietyAfter)
						return MetalineEditResult.Fail;

					//Füge alle Komponenten der nächsten Zeile an das Ende dieser Zeile an
					var lastComponent = Line.components.Count == 0 ? null : Line.components[^1];
					Line.components.AddRange(varietyAfter.components);

					//Prüfe, ob die Komponenten zusammengefügt werden können
					if (lastComponent is not null && varietyAfter.components.Count > 0)
					{
						if (lastComponent.TryMerge(varietyAfter.components[0], int.MaxValue, formatter))
						{
							//Entferne die zusammengefügte Komponente
							Line.components.Remove(varietyAfter.components[0]);
						}
					}

					return new MetalineEditResult(true, new MetalineSelectionRange(this, SimpleRange.CursorAt(lastComponent?.ContentRenderBounds.EndOffset ?? 0)))
					{
						RemoveLineAfter = true,
					};
				}

				if (forward)
					context.SelectionRange = new SimpleRange(context.SelectionRange.Start, context.SelectionRange.Start + 1);
				else
					context.SelectionRange = new SimpleRange(context.SelectionRange.Start - 1, context.SelectionRange.Start);
			}

			return DeleteAndInsertContent(context, formatter, null);
		}

		public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetFormatter? formatter)
		{
			//Wird eine neue Zeile eingefügt?
			if (content == "\n")
			{
				//Dabei Inhalt überschreiben ist nicht erlaubt
				if (context.SelectionRange.Length > 0)
					return MetalineEditResult.Fail;

				//Am Anfang?
				if (context.SelectionRange.Start == 0 && context.SelectionRange.End == 0)
				{
					//Erzeuge eine neue leere Zeile
					var newEmptyLine = new SheetEmptyLine();

					//Füge die neue Zeile davor ein
					return new MetalineEditResult(true, new MetalineSelectionRange(this, SimpleRange.Zero))
					{
						InsertLinesBefore = [newEmptyLine]
					};
				}

				//Finde den Punkt, an dem die Zeile getrennt werden soll
				var index = 0;
				var newLine = new SheetVarietyLine();
				foreach (var component in Line.components)
				{
					//Liegt die Komponente komplett vor dem Bereich?
					if (component.ContentRenderBounds.EndOffset <= context.SelectionRange.Start)
					{
						index++;
						continue;
					}

					//Liegt der Bereich in der Komponente?
					if (component.ContentRenderBounds.StartOffset < context.SelectionRange.Start)
					{
						//Teile die Komponente
						var splitOffset = context.SelectionRange.Start - component.ContentRenderBounds.StartOffset;
						splitOffset = component.ContentRenderBounds.GetContentOffset(splitOffset);
						var end = component.SplitEnd(splitOffset, formatter);

						//Füge das Ende in die neue Zeile ein
						newLine.components.Add(end);
						index++;
						continue;
					}

					//Füge die Komponente in die neue Zeile ein
					newLine.components.Add(component);
				}

				//Ist die neue Zeile leer?
				if (newLine.components.Count == 0)
				{
					//Erzeuge eine neue leere Zeile
					var newEmptyLine = new SheetEmptyLine();

					//Füge die neue Zeile danach ein
					return new MetalineEditResult(true, new MetalineSelectionRange(newEmptyLine, SimpleRange.Zero))
					{
						InsertLinesAfter = [newEmptyLine]
					};
				}

				//Entferne alle verschobenen Komponenten
				Line.components.RemoveRange(index, Line.components.Count - index);

				//Füge die neue Zeile danach ein
				return new MetalineEditResult(true, new MetalineSelectionRange(newLine.ContentEditor, SimpleRange.Zero))
				{
					InsertLinesAfter = [newLine]
				};
			}
			else if (content.Contains('\n'))
			{
				//Zeilenumbrüche müssen einzeln eingefügt werden
				return MetalineEditResult.Fail;
			}

			return DeleteAndInsertContent(context, formatter, content);
		}

		private MetalineEditResult DeleteAndInsertContent(SheetDisplayLineEditingContext context, ISheetFormatter? formatter, string? content)
		{
			//Finde alle Komponente im Bereich
			Component? before = null;
			int beforeIndex = -1;
			Component? after = null;
			int afterIndex = -1;
			List<Component> fullyInside = new();
			var rangeStartsOnComponent = false;
			var rangeEndsOnComponent = false;
			var index = -1;
			foreach (var component in Line.components)
			{
				index++;

				//Beginnt die Komponente vor dem Bereich?
				if (component.ContentRenderBounds.StartOffset < context.SelectionRange.Start)
				{
					//Die Komponente ist der linke Rand
					before = component;
					beforeIndex = index;
				}

				//Liegt die Komponente komplett vor dem Bereich?
				if (component.ContentRenderBounds.EndOffset < context.SelectionRange.Start)
					continue;

				//Liegt die Komponente komplett hinter dem Bereich?
				if (component.ContentRenderBounds.StartOffset > context.SelectionRange.End)
					break;

				//Beginnt die Komponente mit dem Bereich?
				if (component.ContentRenderBounds.StartOffset == context.SelectionRange.Start)
					rangeStartsOnComponent = true;

				//Endet die Komponente mit dem Bereich?
				if (component.ContentRenderBounds.EndOffset == context.SelectionRange.End)
					rangeEndsOnComponent = true;

				//Endet die Komponente nach dem Bereich?
				if (after is null && component.ContentRenderBounds.EndOffset > context.SelectionRange.End)
				{
					//Die Komponente ist der rechte Rand
					after = component;
					afterIndex = index;
				}

				//Liegt die Komponente komplett im Bereich?
				if (component != before && component != after
					&& component.ContentRenderBounds.StartOffset >= context.SelectionRange.Start && component.ContentRenderBounds.EndOffset <= context.SelectionRange.End)
				{
					//Die Komponente liegt im Bereich
					fullyInside.Add(component);
				}
			}

			//Prüfe auf Änderungen
			var removedAnything = fullyInside.Count != 0;
			var addedContentLength = content?.Length ?? 0;

			//Sonderfall: wird ein Text unter einem Leerzeichen-Attachment eingegeben?
			List<VarietyComponent>? newContentComponents = null;
			if (content is not null && after is VarietyComponent varietyAfter && varietyAfter.Content.IsSpace && varietyAfter.Attachments.Count == 1)
			{
				//Erzeuge Komponenten für den Inhalt
				newContentComponents ??= CreateComponentsForContent(content);

				//Hat der Inhalt genau eine Textkomponente?
				if (newContentComponents.Count == 1 && !newContentComponents[0].Content.IsSpace)
				{
					//Übernimm das Attachment des Leerzeichens
					var attachment = varietyAfter.Attachments[0];
					varietyAfter.RemoveAttachment(attachment);
					newContentComponents[0].AddAttachment(attachment);

					//Füge die Komponenten vor dem rechten Rand ein
					Line.components.Insert(afterIndex, newContentComponents[0]);

					//Füge ggf. ein Leerzeichen vor den Inhalt ein
					if (before is VarietyComponent varietyBefore && !varietyBefore.Content.IsSpace)
						Line.components.Insert(afterIndex, new VarietyComponent(" "));

					//Anfügen erfolgreich
					content = null;
				}
			}

			//Sonderfall: wird ein Text mit Attachment durch ein Leerzeichen ersetzt?
			if (content is not null && rangeStartsOnComponent && rangeEndsOnComponent && fullyInside.Count == 1
				&& fullyInside[0] is VarietyComponent varietyInside && !varietyInside.Content.IsSpace && string.IsNullOrWhiteSpace(content))
			{
				//Erzeuge Komponenten für den Inhalt
				newContentComponents ??= CreateComponentsForContent(content);
				
				//Hat der Inhalt genau eine Leerzeichenkomponente?
				if (newContentComponents.Count == 1 && newContentComponents[0].Content.IsSpace)
				{
					//Übernimm das Attachment des Textes
					var attachment = varietyInside.Attachments[0];
					varietyInside.RemoveAttachment(attachment);
					newContentComponents[0].AddAttachment(attachment);

					//Ersetze den Text durch das Leerzeichen
					var insideIndex = beforeIndex != -1 ? beforeIndex + 1
						: afterIndex != -1 ? afterIndex - 1
						: Line.components.IndexOf(fullyInside[0]);
					Line.components[insideIndex] = newContentComponents[0];

					//Anfügen erfolgreich
					content = null;
				}
			}

			//Entferne Überlappung am linken Rand
			var skipTrimAfter = false;
			if (before is not null)
			{
				//Kürze die Komponente
				var tailLength = before.ContentRenderBounds.GetContentOffset(context.SelectionRange.Start);
				if (before.TryRemoveContent(tailLength, context.SelectionRange.Length, formatter))
					removedAnything = true;

				//Gibt es einen Inhalt?
				if (content is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					var repeat = false;
					do
					{
						//Sind linker und rechter Rand gleich und der Inhalt kann nicht einfach so hinzugefügt werden?
						if (after == before && (newContentComponents.Count > 1 || repeat))
						{
							//Teile die Randkomponente in zwei Teile
							//TODO: Wenn ein automatischer Bindestrich angezeigt wird, funktioniert das Einfügen dirent davor nicht richtig
							var newAfter = before.SplitEnd(before.ContentRenderBounds.GetOffsetWithGaps(tailLength), formatter);

							//Füge den neuen rechten Rand ein
							if (beforeIndex == Line.components.Count - 1)
								Line.components.Add(newAfter);
							else
								Line.components.Insert(beforeIndex + 1, newAfter);

							//Der rechte Rand muss jetzt nicht mehr gekürzt werden
							after = newAfter;
							afterIndex = beforeIndex + 1;
							skipTrimAfter = true;
						}

						//Versuche die erste Komponente hinten an den linken Rand anzufügen
						if (before.TryMerge(newContentComponents[0], tailLength, formatter))
						{
							//Erste Komponente hinzugefügt
							newContentComponents.RemoveAt(0);

							//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
							if (newContentComponents.Count > 0 && after?.TryMerge(newContentComponents[^1], 0, formatter) == true)
							{
								//Letzte Komponente hinzugefügt
								newContentComponents.RemoveAt(newContentComponents.Count - 1);
							}

							//Füge die restlichen Komponenten dazwischen ein
							if (newContentComponents.Count > 0)
							{
								if (beforeIndex == Line.components.Count - 1)
									Line.components.AddRange(newContentComponents);
								else
									Line.components.InsertRange(beforeIndex + 1, newContentComponents);
							}

							//Anfügen erfolgreich
							content = null;
						}
						else
						{
							//Der Rand muss aufgetrennt werden
							repeat = true;
						}
					}
					while (before == after && repeat);
				}
			}

			//Gibt es einen Inhalt und beginnt der Bereich mit einer Komponente?
			if (content is not null && rangeStartsOnComponent)
			{
				//Versuche den Inhalt an den Anfang in diese Komponente anzufügen
				var firstFullyInside = fullyInside.FirstOrDefault();
				if (firstFullyInside is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					//Besteht der Inhalt nur aus einer Komponente?
					if (newContentComponents.Count == 1)
					{
						//Versuche die Komponente vorne an die erste innere anzufügen
						if (firstFullyInside.TryMerge(newContentComponents[0], 0, formatter))
						{
							//Kürze den Inhalt der ersten Komponente
							firstFullyInside.TryRemoveContent(content.Length, int.MaxValue, formatter);

							//Entferne die Komponente doch nicht
							fullyInside.RemoveAt(0);

							//Anfügen erfolgreich
							content = null;
						}
					}
				}
			}

			//Entferne Überlappung am rechten Rand
			if (after is not null && after != before)
			{
				//Kürze die Komponente
				var overlap = after.ContentRenderBounds.GetContentOffset(context.SelectionRange.End);
				if (!skipTrimAfter && after.TryRemoveContent(0, overlap, formatter))
					removedAnything = true;

				//Gibt es einen Inhalt?
				if (content is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
					if (after.TryMerge(newContentComponents[^1], 0, formatter))
					{
						//Erste Komponente hinzugefügt
						newContentComponents.RemoveAt(newContentComponents.Count - 1);

						//Füge die restlichen Komponenten davor ein
						if (newContentComponents.Count > 0)
						{
							Line.components.InsertRange(afterIndex, newContentComponents);
						}

						//Anfügen erfolgreich
						content = null;
					}
				}
			}

			//Entferne alle Komponenten, die komplett im Bereich liegen
			Line.components.RemoveAll(fullyInside.Contains);

			//Wurde der Inhalt immer noch nicht hinzugefügt?
			if (content is not null)
			{
				//Erzeuge Komponenten für den Inhalt
				newContentComponents ??= CreateComponentsForContent(content);

				//Füge die Komponenten nach dem linken Rand ein
				var insertIndex = beforeIndex == -1 ? afterIndex : beforeIndex + 1;
				if (insertIndex < 0 || insertIndex >= Line.components.Count)
					Line.components.AddRange(newContentComponents);
				else
					Line.components.InsertRange(insertIndex, newContentComponents);

				//Hinzufügen erfolgreich
				content = null;
			}

			//Prüfe, ob der linke Rand entfernt werden kann
			if (before is not null && before.IsEmpty)
			{
				//Entferne die linke Randkomponente
				Line.components.Remove(before);
				removedAnything = true;
			}

			//Prüfe, ob der rechte Rand entfernt werden kann
			if (after is not null && after != before && after.IsEmpty)
			{
				//Entferne die rechte Randkomponente
				Line.components.Remove(after);
				removedAnything = true;
			}

			//Prüfe, ob Komponenten zusammengefügt werden können. Beginne mit der Komponente links vom linken Rand
			var stopCombining = false;
			for (var current = Math.Max(beforeIndex - 1, 0); current < Line.components.Count - 1 && !stopCombining; current++)
			{
				//Kann die Komponente mit der darauf folgenden zusammengeführt werden?
				var component = Line.components[current];
				var nextComponent = Line.components[current + 1];
				if (component.TryMerge(nextComponent, int.MaxValue, formatter))
				{
					//Entferne die folgende Komponente
					Line.components.RemoveAt(current + 1);
					removedAnything = true;
					current--;
				}

				//Höre nach dem rechten Rand auf
				if (nextComponent == after)
					stopCombining = true;
			}

			//Nicht erfolgreich?
			if (!removedAnything && addedContentLength == 0)
				return MetalineEditResult.Fail;

			//Modified-Event
			Line.RaiseModified(new ModifiedEventArgs(Line));

			//Ist die Zeile jetzt leer?
			if (Line.components.Count == 0)
			{
				//Ersetze die Zeile durch eine Leerzeile
				var newLine = new SheetEmptyLine();
				return new MetalineEditResult(true, new(newLine, SimpleRange.CursorAt(0)))
				{
					RemoveLine = true,
					InsertLinesBefore = [newLine]
				};
			}

			return this.CreateSuccessEditResult(SimpleRange.CursorAt(context.SelectionRange.Start + addedContentLength));
		}

		private static List<VarietyComponent> CreateComponentsForContent(string content)
		{
			if (string.IsNullOrEmpty(content))
				throw new InvalidOperationException("Tried to insert empty content");

			//Trenne Wörter
			var result = content.SplitAlternating(char.IsWhiteSpace)
				.Select(w => VarietyComponent.FromString(w))
				.ToList();

			//var result = new List<VarietyComponent>();
			//var index = 0;
			//do
			//{
			//	//Finde Länge des aktuellen Typs (Wort oder Leerstelle)
			//	var isSpace = char.IsWhiteSpace(content[index]);
			//	var length = content.Skip(index + 1)
			//		.Select((c, i) => (Char: c, Index: i + 1))
			//		.SkipWhile(c => char.IsWhiteSpace(c.Char) == isSpace)
			//		.Select(c => (int?)c.Index)
			//		.FirstOrDefault()
			//		?? content.Length - index;

			//	//Erzeuge ein Wort der entsprechenden Länge
			//	var word = content.Substring(index, length);
			//	result.Add(VarietyComponent.FromString(word));
			//	index += length;

			//	//Safeguard: length darf nie 0 sein
			//	if (length == 0)
			//		throw new InvalidOperationException("Error creating content components");
			//}
			//while (index < content.Length);

			return result;
		}
	}

	private class AttachmentEditing : ISheetDisplayLineEditing
	{
		public SheetVarietyLine Line { get; }
		SheetLine ISheetDisplayLineEditing.Line => Line;

		public int LineId => 1;

		public AttachmentEditing(SheetVarietyLine owner)
		{
			this.Line = owner;
		}

		public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetFormatter? formatter = null)
		{
			//Ist der Bereich leer?
			if (context.SelectionRange.Length == 0)
			{
				if (forward)
					context.SelectionRange = new SimpleRange(context.SelectionRange.Start, context.SelectionRange.Start + 1);
				else
					context.SelectionRange = new SimpleRange(context.SelectionRange.Start - 1, context.SelectionRange.Start);
			}

			//Finde alle Attachments im Bereich
			var attachments = FindAttachmentsInRange(context.SelectionRange, formatter).ToList();

			//Liegt genau ein Attachment im Bereich?
			if (attachments.Count == 1)
			{
				//Bearbeite das Attachment
				return EditAttachment(attachments[0].Component, attachments[0].Attachment, context.SelectionRange, null, formatter);
			}

			//Liegen mehrere Attachments im Bereich?
			if (attachments.Count > 1)
			{
				//Liegen sie nicht alle komplett im Bereich?
				if (!attachments.All(a => a.Attachment.RenderBounds.StartOffset >= context.SelectionRange.Start && a.Attachment.RenderBounds.EndOffset <= context.SelectionRange.End))
					return MetalineEditResult.Fail;

				//Lösche die Attachments
				foreach (var (component, attachment) in attachments)
					component.RemoveAttachment(attachment);

				//Modified-Event
				Line.RaiseModified(new ModifiedEventArgs(Line));
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(context.SelectionRange.Start));
			}

			//Finde das nächste Attachment und verschiebe es nach links
			return FindAndMoveNextAttachment(context.SelectionRange.Start, -context.SelectionRange.Length, formatter);
		}

		public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetFormatter? formatter)
		{
			//Finde alle Attachments im Bereich
			var attachments = FindAttachmentsInRange(context.SelectionRange, formatter).ToList();

			//Liegt genau ein Attachment im Bereich?
			if (attachments.Count == 1)
			{
				//Bearbeite das Attachment
				return EditAttachment(attachments[0].Component, attachments[0].Attachment, context.SelectionRange, content, formatter);
			}

			//Liegt mehr als ein Attachment im Bereich?
			if (attachments.Count > 1)
			{
				//Bearbeiten von mehreren Attachments nicht möglich
				return MetalineEditResult.Fail;
			}

			//Werden Whitespaces eingefügt?
			if (string.IsNullOrWhiteSpace(content))
			{
				//Finde das nächste Attachment und verschiebe es nach rechts
				var moveOffset = content.Length - context.SelectionRange.Length;
				return FindAndMoveNextAttachment(context.SelectionRange.Start, moveOffset, formatter);
			}

			//Ist der Bereich eine Selektion?
			if (context.SelectionRange.Length > 0)
				return MetalineEditResult.Fail;

			//Finde die Komponente, in der der Inhalt eingefügt werden soll
			var component = Line.components.OfType<VarietyComponent>()
				.FirstOrDefault(c => c.ContentRenderBounds.StartOffset <= context.SelectionRange.Start && c.ContentRenderBounds.EndOffset > context.SelectionRange.End);
			if (component is null)
				return MetalineEditResult.Fail;

			//Liegt ein Attachment direkt vor oder hinter der Position?
			var before = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().LastOrDefault(a => a.RenderBounds.EndOffset == context.SelectionRange.Start - 1);
			var after = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().FirstOrDefault(a => a.RenderBounds.StartOffset == context.SelectionRange.End);
			if (before is not null)
			{
				//Füge den Inhalt ans Ende des Attachments an
				before.InsertContent(content, int.MaxValue, formatter);

				//Modified-Event
				Line.RaiseModified(new ModifiedEventArgs(Line));
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(context.SelectionRange.Start + content.Length));
			}
			else if (after is not null)
			{
				//Füge den Inhalt an den Anfang des Attachments an
				after.InsertContent(content, 0, formatter);

				//Modified-Event
				Line.RaiseModified(new ModifiedEventArgs(Line));
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(context.SelectionRange.Start + content.Length));
			}

			//Erzeuge ein neues Attachment
			var newAttachment = VarietyComponent.VarietyAttachment.FromString(component.ContentRenderBounds.GetContentOffset(context.SelectionRange.Start), content);
			component.AddAttachment(newAttachment);

			//Modified-Event
			Line.RaiseModified(new ModifiedEventArgs(Line));
			return this.CreateSuccessEditResult(SimpleRange.CursorAt(context.SelectionRange.Start + content.Length));
		}

		private IEnumerable<(VarietyComponent Component, VarietyComponent.VarietyAttachment Attachment)> FindAttachmentsInRange(SimpleRange range, ISheetFormatter? formatter)
		{
			//Finde alle Attachments im Bereich
			foreach (var component in Line.components)
			{
				//Liegt die Komponente vor dem Bereich?
				if (component.TotalRenderBounds.EndOffset <= range.Start)
					continue;

				//Liegt die Komponente hinter dem Bereich?
				if (component.TotalRenderBounds.StartOffset >= range.End)
					break;

				//Ist die Komponente keine VarietyComponent?
				if (component is not VarietyComponent varietyComponent)
					continue;

				//Gehe durch alle Attachments
				foreach (var attachment in varietyComponent.Attachments)
				{
					//Liegt das Attachment vor dem Bereich?
					if (attachment.RenderBounds.EndOffset <= range.Start)
						continue;

					//Liegt das Attachment hinter dem Bereich?
					if (attachment.RenderBounds.StartOffset >= range.End)
						break;

					//Ist das Attachment kein VarietyComponent?
					if (attachment is not VarietyComponent.VarietyAttachment varietyAttachment)
						continue;

					yield return (varietyComponent, varietyAttachment);
				}
			}
		}

		private MetalineEditResult EditAttachment(VarietyComponent component, VarietyComponent.VarietyAttachment attachment, SimpleRange selectionRange, string? content, ISheetFormatter? formatter)
		{
			//Kürze das Attachment
			var startOffset = selectionRange.Start - attachment.RenderBounds.StartOffset;
			if (selectionRange.Length > 0)
				attachment.TryRemoveContent(startOffset, selectionRange.Length, formatter);
			
			//Füge den Inhalt ein
			if (content is not null)
				attachment.InsertContent(content, startOffset, formatter);

			//Entferne das Attachment, falls es jetzt leer ist
			if (attachment.IsEmpty)
				component.RemoveAttachment(attachment);

			//Modified-Event
			Line.RaiseModified(new ModifiedEventArgs(Line));

			//Fertig
			return this.CreateSuccessEditResult(SimpleRange.CursorAt(selectionRange.Start + (content?.Length ?? 0)));
		}

		private MetalineEditResult FindAndMoveNextAttachment(int startOffset, int moveOffset, ISheetFormatter? formatter)
		{
			//Finde das nächste Attachment
			var nextAttachment = FindAttachmentsInRange(new SimpleRange(startOffset, int.MaxValue), formatter).FirstOrDefault();
			if (nextAttachment.Attachment is null)
				return MetalineEditResult.Fail;

			//Berechne den neuen Offset
			var newOffset = nextAttachment.Attachment.Offset + moveOffset;
			if (newOffset < 0)
				newOffset = 0;
			else
			{
				var contentLength = nextAttachment.Component.Content.GetLength(formatter);
				if (newOffset >= contentLength)
					newOffset = contentLength - 1;
			}

			//Hat sich der Offset nicht verändert?
			if (newOffset == nextAttachment.Attachment.Offset)
				return MetalineEditResult.Fail;

			//Verschiebe das Attachment
			nextAttachment.Attachment.SetOffset(newOffset);
			
			//Modified-Event
			Line.RaiseModified(new ModifiedEventArgs(Line));
			return this.CreateSuccessEditResult(SimpleRange.CursorAt(moveOffset < 0 ? startOffset : startOffset + moveOffset));
		}

		//private LineEditResult DeleteAndInsertContent(SimpleRange selectionRange, ISheetFormatter? formatter, string? content)
		//{
		//	//Finde alle Attachments im Bereich
		//	(VarietyComponent Component, VarietyAttachment Attachment)? before = null;
		//	(VarietyComponent Component, VarietyAttachment Attachment)? after = null;
		//	List<(VarietyComponent Component, VarietyAttachment Attachment)> fullyInside = new();
		//	VarietyComponent? selectionStartComponent = null;
		//	var rangeStartsOnAttachment = false;
		//	var index = -1;
		//	foreach (var component in owner.components)
		//	{
		//		//Liegt die Komponente komplett vor dem Bereich?
		//		if (component.TotalRenderBounds.EndOffset < selectionRange.Start)
		//			continue;

		//		//Liegt die Komponente komplett hinter dem Bereich?
		//		if (component.TotalRenderBounds.StartOffset > selectionRange.End)
		//			break;

		//		//Ist die Komponente keine VarietyComponent?
		//		if (component is not VarietyComponent varietyComponent)
		//			return LineEditResult.Fail;

		//		//Liegt der Beginn des Bereichs in dieser Komponente?
		//		if (selectionStartComponent is null)
		//			selectionStartComponent = varietyComponent;

		//		//Gehe durch alle Attachments
		//		foreach (var attachment in varietyComponent.Attachments)
		//		{
		//			//Liegt das Attachment komplett vor dem Bereich?
		//			if (attachment.RenderBounds.EndOffset < selectionRange.Start)
		//				continue;

		//			//Liegt das Attachment komplett hinter dem Bereich?
		//			if (attachment.RenderBounds.StartOffset > selectionRange.End)
		//				break;

		//			//das Attachment kein VarietyComponent?
		//			if (attachment is not VarietyAttachment varietyAttachment)
		//				return LineEditResult.Fail;

		//			//Beginnt das Attachment vor dem Bereich?
		//			if (before is null && varietyAttachment.RenderBounds.StartOffset < selectionRange.Start)
		//			{
		//				//Die Komponente ist der linke Rand
		//				before = (varietyComponent, varietyAttachment);
		//			}

		//			//Beginnt das Attachment mit dem Bereich?
		//			if (varietyAttachment.RenderBounds.StartOffset == selectionRange.Start)
		//				rangeStartsOnAttachment = true;

		//			//Endet das Attachment nach dem Bereich?
		//			if (after is null && varietyAttachment.RenderBounds.EndOffset > selectionRange.End)
		//			{
		//				//Das Attachment ist der rechte Rand
		//				after = (varietyComponent, varietyAttachment);
		//			}

		//			//Liegt das Attachment komplett im Bereich?
		//			if (varietyAttachment != before?.Attachment && varietyAttachment != after?.Attachment
		//				&& varietyAttachment.RenderBounds.StartOffset >= selectionRange.Start && varietyAttachment.RenderBounds.EndOffset <= selectionRange.End)
		//			{
		//				//Das Attachment liegt im Bereich
		//				fullyInside.Add((varietyComponent, varietyAttachment));
		//			}
		//		}
		//	}

		//	//Prüfe auf Änderungen
		//	var removedAnything = fullyInside.Count != 0;
		//	var contentAdded = false;

		//	//Trenne den Textinhalt an Whitespaces
		//	var contentSplit = content is not null && string.IsNullOrWhiteSpace(content) ? [content]
		//		: content?.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

		//	//Sonderfall: mehrere Attachments auf einmal
		//	if (contentSplit?.Length > 1)
		//	{
		//		//Die Anzahl muss übereinstimmen und alle Attachments müssen im Bereich liegen
		//		if (before is not null || after is not null || fullyInside.Count != contentSplit.Length)
		//			return LineEditResult.Fail;

		//		//Gehe durch die Attachments und ersetze sie
		//		foreach ((var attachment, var newContent) in fullyInside.Zip(contentSplit))
		//		{
		//			//Ersetze das Attachment
		//			attachment.Attachment.ReplaceContent(newContent, formatter);
		//		}

		//		//Fertig
		//		return new LineEditResult(true, new SimpleRange(selectionRange.Start, selectionRange.Start));
		//	}

		//	//Content muss aus einem Teil bestehen
		//	var contentText = contentSplit is null ? null : contentSplit[0];

		//	//Hinzufügen nur möglich, wenn nicht mehr als ein Attachment bearbeitet wird
		//	if (contentText is not null)
		//	{
		//		var totalEditCount = fullyInside.Count;
		//		if (before is not null)
		//			totalEditCount++;
		//		if (after is not null && after.Value.Attachment != before?.Attachment)
		//			totalEditCount++;
		//		if (totalEditCount > 1)
		//			return LineEditResult.Fail;
		//	}

		//	//Gibt es eine Überlappung am linken Rand?
		//	if (before is not null)
		//	{
		//		//Kürze das Attachment
		//		var tailLength = selectionRange.Start - before.Value.Attachment.RenderBounds.StartOffset;
		//		if (before.Value.Attachment.TryRemoveContent(tailLength, selectionRange.Length, formatter))
		//			removedAnything = true;

		//		//Gibt es einen Inhalt?
		//		if (contentText is not null)
		//		{
		//			//Versuche den Inhalt hinzuzufügen
		//			if (TryAddContent(selectionRange, before.Value.Component, before.Value.Attachment, contentText, formatter))
		//			{
		//				contentAdded = true;
		//				contentText = null;
		//			}
		//		}
		//	}

		//	//Gibt es einen Inhalt und beginnt der Bereich mit einem Attachment?
		//	if (contentText is not null && rangeStartsOnAttachment)
		//	{
		//		//Füge den Inhalt am Anfang des Attachments ein
		//		var firstFullyInside = fullyInside.FirstOrDefault();
		//		if (firstFullyInside.Component is not null)
		//		{
		//			//Versuche den Inhalt hinzuzufügen
		//			if (TryAddContent(selectionRange, firstFullyInside.Component, firstFullyInside.Attachment, contentText, formatter))
		//			{
		//				contentAdded = true;
		//				contentText = null;

		//				//Das Attachment muss nicht mehr bearbeitet werden
		//				fullyInside.RemoveAt(0);
		//			}
		//		}
		//	}

		//	//Gibt es eine Überlappung am rechten Rand?
		//	if (after is not null && after.Value.Attachment != before?.Attachment)
		//	{
		//		//Kürze das Attachment
		//		var overlap = selectionRange.End - after.Value.Attachment.RenderBounds.StartOffset;
		//		if (after.Value.Attachment.TryRemoveContent(0, overlap, formatter))
		//			removedAnything = true;

		//		//Gibt es einen Inhalt?
		//		if (contentText is not null)
		//		{
		//			//Versuche den Inhalt hinzuzufügen
		//			if (TryAddContent(selectionRange, after.Value.Component, after.Value.Attachment, contentText, formatter))
		//			{
		//				contentAdded = true;
		//				contentText = null;
		//			}
		//		}
		//	}

		//	//Entferne alle Attachments, die komplett im Bereich liegen
		//	foreach (var (component, attachment) in fullyInside)
		//		component.RemoveAttachment(attachment);

		//	//Wurde der Content immer noch nicht hinzugefügt?
		//	if (contentText is not null)
		//	{
		//		if (!string.IsNullOrWhiteSpace(contentText) && selectionStartComponent is not null)
		//		{
		//			//Erzeuge ein neues Attachment
		//			var newAttachment = new VarietyAttachment(selectionRange.Start - selectionStartComponent.ContentRenderBounds.StartOffset, contentText);
		//			selectionStartComponent.AddAttachment(newAttachment);
		//			contentAdded = true;
		//			contentText = null;
		//		}
		//	}

		//	//Prüfe, ob Attachments zusammengefügt werden sollen
		//	if (before is not null && before.Value.Attachment != after?.Attachment
		//		&& before.Value.Component == after?.Component)
		//	{
		//		//Versuche die Komponenten zusammenzufügen
		//		if (before.Value.Attachment.TryMergeContents(after.Value.Attachment, formatter))
		//		{
		//			//Entferne das rechte Attachment
		//			after.Value.Component.RemoveAttachment(after.Value.Attachment);
		//			removedAnything = true;
		//			before = null;
		//		}
		//	}

		//	//Prüfe, ob der linke Rand entfernt werden kann
		//	if (before is not null && before.Value.Attachment.IsEmpty)
		//	{
		//		//Entferne das linke Randattachment
		//		before.Value.Component.RemoveAttachment(before.Value.Attachment);
		//		removedAnything = true;
		//	}

		//	//Prüfe, ob der rechte Rand entfernt werden kann
		//	if (after is not null && after.Value.Attachment != before?.Attachment && after.Value.Attachment.IsEmpty)
		//	{
		//		//Entferne das rechte Randattachment
		//		after.Value.Component.RemoveAttachment(after.Value.Attachment);
		//		removedAnything = true;
		//	}

		//	//Nicht erfolgreich?
		//	if (!removedAnything && !contentAdded)
		//		return LineEditResult.Fail;

		//	//Modified-Event
		//	owner.RaiseModified(new ModifiedEventArgs(owner));
		//	var selectionOffset = selectionRange.Start + (contentText?.Length ?? 0);
		//	return new LineEditResult(true, new SimpleRange(selectionOffset, selectionOffset));
		//}

		//private static bool TryAddContent(SimpleRange selectionRange, VarietyComponent component, VarietyAttachment attachment, string contentText, ISheetFormatter? formatter)
		//{
		//	//Whitespace?
		//	if (string.IsNullOrWhiteSpace(contentText))
		//	{
		//		//Direkt am Anfang des Attachments?
		//		if (selectionRange.Start == attachment.RenderBounds.StartOffset)
		//		{
		//			//Verschiebe das Attachment nach rechts
		//			attachment.SetOffset(attachment.Offset + 1);

		//			//Content wurde hinzugefügt
		//			return true;
		//		}

		//		//Trenne das Attachment und füge ggf. das neue Attachment hinzu
		//		var newAttachment = attachment.SplitEnd(selectionRange.Start - attachment.RenderBounds.StartOffset, formatter);
		//		if (newAttachment is not null)
		//		{
		//			//Verschiebe das Attachment um eins nach hinten
		//			newAttachment.SetOffset(newAttachment.Offset + 1);
		//			component.AddAttachment(newAttachment);

		//			//Content wurde hinzugefügt
		//			return true;
		//		}
		//	}
		//	else
		//	{
		//		//Füge den Inhalt hinzu
		//		attachment.InsertContent(contentText, selectionRange.Start - attachment.RenderBounds.StartOffset, formatter);

		//		//Content wurde hinzugefügt
		//		return true;
		//	}

		//	return false;
		//}
	}
	#endregion

	#region Content
	[Flags]
	public enum SpecialContentType
	{
		None = 0,
		Chord = 1,
	}

	internal class ComponentContent
	{
		public string? Text { get; private set; }
		public Chord? Chord { get; private set; }

		public SpecialContentType AllowedTypes { get; }
		public SpecialContentType CurrentType => Chord is not null ? SpecialContentType.Chord : SpecialContentType.None;

		public bool IsEmpty => Chord is null && string.IsNullOrEmpty(Text);
		public bool IsSpace => Chord is null && string.IsNullOrWhiteSpace(Text);

		public ComponentContent(string text, SpecialContentType allowedTypes = SpecialContentType.Chord)
		{
			Text = text;
			AllowedTypes = allowedTypes;
		}

		public ComponentContent(Chord chord, SpecialContentType allowedTypes = SpecialContentType.Chord)
		{
			Chord = chord;
			AllowedTypes = allowedTypes;
		}

		public static ComponentContent FromString(string content, SpecialContentType allowedTypes = SpecialContentType.Chord)
		{
			var result = new ComponentContent(content, allowedTypes);
			result.SetContent(content);
			return result;
		}

		public static ComponentContent CreateSpace(int length, SpecialContentType allowedTypes = SpecialContentType.Chord)
			=> new ComponentContent(new string(' ', length), allowedTypes);

		public SheetDisplayLineElement CreateElement(SheetDisplaySliceInfo sliceInfo, ISheetFormatter? formatter)
		{
			if (Chord is not null)
				return new SheetDisplayLineChord(Chord)
				{
					Slice = sliceInfo
				};
			else if (string.IsNullOrWhiteSpace(Text))
				return new SheetDisplayLineSpace(Text?.Length ?? 0)
				{
					Slice = sliceInfo
				};
			else
				return new SheetDisplayLineText(Text ?? string.Empty)
				{
					Slice = sliceInfo
				};
		}

		public SheetDisplayLineElement CreateElement(SheetDisplaySliceInfo sliceId, int offset, int length, ISheetFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			if (offset == 0 && length >= textContent.Length)
				return CreateElement(sliceId, formatter);

			//Bilde Substring
			var subContent = FromString(textContent.Substring(offset, Math.Min(length, textContent.Length - offset)), CurrentType);
			return subContent.CreateElement(sliceId, formatter);
		}

		public int GetLength(ISheetFormatter? formatter)
			=> Text?.Length
			?? Chord?.ToString(formatter).Length
			?? 0;

		public ComponentContent GetContent(int offset, int length, ISheetFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			if (offset == 0 && length >= textContent.Length)
				return this;

			//Bilde Substring
			return FromString(textContent.Substring(offset, Math.Min(length, textContent.Length - offset)), CurrentType);
		}

		internal bool RemoveContent(int offset, int length, ISheetFormatter? formatter)
		{
			if (offset < 0)
			{
				length -= offset;
				offset = 0;
			}

			if (length <= 0)
				return false;

			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter);
			if (textContent is null) return false;

			//Kürze den Textinhalt
			if (offset >= textContent.Length) return false;
			var newContent = textContent.Remove(offset, Math.Min(length, textContent.Length - offset));
			if (newContent == textContent) return false;

			//Setze den neuen Inhalt
			SetContent(newContent);
			return true;
		}

		internal void AppendContent(string content, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter);

			//Füge den Textinhalt hinzu
			var newContent = textContent + content;
			SetContent(newContent);
		}

		internal void MergeContents(string content, int offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset, textContent.Length);
			var newContent = textContent[0..stringOffset] + content;
			if (offset < textContent.Length)
				newContent += textContent[stringOffset..];
			SetContent(newContent);
		}

		internal void MergeContents(ComponentContent content, int offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			var afterTextContent = content.Text ?? content.Chord?.ToString(formatter);

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset, textContent.Length);
			var newContent = textContent[0..stringOffset] + afterTextContent;
			if (offset < textContent.Length)
				newContent += textContent[stringOffset..];
			SetContent(newContent);
		}

		internal ComponentContent SplitEnd(int offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;

			//Trenne den Textinhalt auf
			var newContent = textContent[..offset];
			var newEndContent = textContent[offset..];

			//Setze den neuen Inhalt
			SetContent(newContent);

			//Erzeuge das neue Ende
			return ComponentContent.FromString(newEndContent);
		}

		internal void SetContent(string content)
		{
			if ((AllowedTypes & SpecialContentType.Chord) != 0)
			{
				//Versuche den Inhalt als Akkord zu lesen
				var chordLength = Chord.TryRead(content, out var chord);
				if (chord is not null && chordLength == content.Length)
				{
					//Der Inhalt ist ein Akkord
					Chord = chord;
					Text = null;
					return;
				}
			}

			//Der Inhalt ist kein Akkord
			Text = content;
			Chord = null;
		}

		#region Operators
		public override string ToString() => Text ?? Chord?.ToString() ?? string.Empty;
		#endregion
	}
	#endregion

	#region Components
	internal readonly record struct RenderBounds(int StartOffset, int EndOffset)
	{
		public static readonly RenderBounds Empty = new(0, 0);

		public int Length => EndOffset - StartOffset;
	}

	internal record GapRenderBounds(int StartOffset, int EndOffset)
	{
		public static readonly GapRenderBounds Empty = new(0, 0);

		public int Length => EndOffset - StartOffset;

		public IReadOnlyList<RenderGap> Gaps { get; init; } = [];

		public int GetContentOffset(int offset)
		{
			var contentOffset = offset - StartOffset;
			foreach (var gap in Gaps)
			{
				//Liegt die Lücke nach dem Offset?
				if (gap.StartOffset > offset)
					continue;

				//Wie weit liegt das Ende der Lücke hinter dem Offset?
				var diff = gap.EndOffset - offset;
				if (diff <= 0)
					contentOffset -= gap.Length;
				else
					contentOffset -= diff;
			}

			return contentOffset;
		}

		public int GetOffsetWithGaps(int offset)
			=> GetContentOffset(offset) + StartOffset;

		public int GetContentOffsetFromEnd(int offset)
		{
			var contentOffset = EndOffset - offset;
			foreach (var gap in Gaps)
			{
				//Liegt die Lücke vor dem Offset?
				if (gap.EndOffset > offset)
					continue;

				//Wie weit liegt der Anfang der Lücke vor dem Offset?
				var diff = offset - gap.StartOffset;
				if (diff <= 0)
					contentOffset += gap.Length;
				else
					contentOffset += diff;
			}

			return contentOffset;
		}
	}

	internal record RenderGap(int StartOffset, int EndOffset)
	{
		public int Length => EndOffset - StartOffset;

		public static RenderGap FromLength(int startOffset, int length)
			=> new(startOffset, startOffset + length);
	}

	public abstract class Component
	{
		public abstract bool IsEmpty { get; }

		internal abstract GapRenderBounds ContentRenderBounds { get; }
		internal abstract RenderBounds TotalRenderBounds { get; }

		internal abstract void BuildLines(LineBuilders builders, ISheetBuilderFormatter? formatter);

		internal abstract bool TryRemoveContent(int offet, int length, ISheetFormatter? formatter);
		internal abstract bool TryMerge(Component next, int offset, ISheetFormatter? formatter);
		internal abstract Component SplitEnd(int offset, ISheetFormatter? formatter);

		internal class LineBuilders
		{
			public SheetVarietyLine Owner { get; }
			public SheetDisplayTextLine.Builder TextLine { get; }
			public SheetDisplayChordLine.Builder ChordLine { get; }

			public bool IsTitleLine { get; init; }
			public bool IsRenderingTitle { get; set; }

			public int CurrentLength => Math.Max(TextLine.CurrentLength, ChordLine.CurrentLength);

			public LineBuilders(SheetVarietyLine owner, SheetDisplayTextLine.Builder textLine, SheetDisplayChordLine.Builder chordLine)
			{
				Owner = owner;
				TextLine = textLine;
				ChordLine = chordLine;
			}
		}
	}

	public sealed class VarietyComponent : Component
	{
		private readonly List<Attachment> attachments = new();
		public IReadOnlyList<Attachment> Attachments => attachments;

		internal ComponentContent Content { get; }

		public override bool IsEmpty => Content.IsEmpty;

		private GapRenderBounds contentRenderBounds = GapRenderBounds.Empty;
		internal override GapRenderBounds ContentRenderBounds => contentRenderBounds;

		private RenderBounds totalRenderBounds = RenderBounds.Empty;
		internal override RenderBounds TotalRenderBounds => totalRenderBounds;

		private VarietyComponent(ComponentContent content)
		{
			Content = content;
		}

		public VarietyComponent(string text, SpecialContentType allowedTypes = SpecialContentType.None)
		{
			Content = new(text, allowedTypes);
		}

		public VarietyComponent(Chord chord, SpecialContentType allowedTypes = SpecialContentType.Chord)
		{
			Content = new(chord, allowedTypes);
		}

		public static VarietyComponent FromString(string content, SpecialContentType allowedTypes = SpecialContentType.Chord)
			=> new VarietyComponent(ComponentContent.FromString(content, allowedTypes));

		public static VarietyComponent CreateSpace(int length, SpecialContentType allowedTypes = SpecialContentType.None)
			=> new VarietyComponent(ComponentContent.CreateSpace(length, allowedTypes));

		#region Display
		internal override void BuildLines(LineBuilders builders, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Finde das erste Attachment
			(Attachment Attachment, SheetDisplayLineElement Display) firstAttachment = attachments
				.Select((a, i) => (Attachment: a, Display: a.CreateDisplayAttachment(new(i, 0, a.Offset), out _, formatter)))
				.FirstOrDefault(a => a.Display is not null)!;
			if (firstAttachment.Attachment is not null)
			{
				//An welchen Offset soll das Attachment geschrieben werden?
				var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, firstAttachment.Display)
					?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
				var targetOffset = builders.ChordLine.CurrentLength + spaceBefore;

				//Wie viel mehr Platz wird auf der Akkordzeile benötigt, damit das Attachment passt?
				var difference = targetOffset - builders.TextLine.CurrentLength - firstAttachment.Attachment.Offset;

				//Verlängere die Textzeile auch um diese Differenz
				builders.TextLine.ExtendLength(0, difference);
			}

			//Speichere aktuelle Textlänge für Render Bounds
			var textStartIndex = builders.TextLine.CurrentLength;
			var chordStartIndex = builders.ChordLine.CurrentLength;

			//Trenne den Text an Attachments
			Attachment? currentAttachment = null;
			var gaps = new List<RenderGap>();
			var sliceIndex = -2;
			var currentAttachmentSliceIndex = -2;
			foreach (var nextAttachment in attachments.Prepend(new EmptyAttachmentStub(0)).Append(new EmptyAttachmentStub(contentLength)))
			{
				sliceIndex++;

				//Merke das erste Attachment
				if (currentAttachment is null)
				{
					currentAttachment = nextAttachment;
					currentAttachmentSliceIndex = sliceIndex;
					continue;
				}

				//Berechne Textlänge
				var textLength = nextAttachment.Offset - currentAttachment.Offset;
				if (textLength > 0)
				{
					//Wird ein Attachment geschrieben?
					var displayAttachment = currentAttachment.CreateDisplayAttachment(new(currentAttachmentSliceIndex, 0, currentAttachment.Offset),
						out var setAttachmentRenderBounds, formatter);
					if (displayAttachment is not null)
					{
						//Lasse Platz vor dem Attachment
						var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, displayAttachment)
							?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
						builders.ChordLine.ExtendLength(0, spaceBefore);

						//Stelle sicher, dass die Textzeile bisher so lang wie die Akkordzeile ist, um Content und Attachment zusammenzuhalten
						var textLineGap = builders.ChordLine.CurrentLength - builders.TextLine.CurrentLength;

						//Berechne die Lücke
						if (textLineGap > 0)
						{
							//Schreibe den Text vor dem Attachment
							var offsetBefore = builders.TextLine.CurrentLength;
							builders.TextLine.Append(new SheetDisplayLineHyphen(textLineGap)
							{
								Slice = new(currentAttachmentSliceIndex - 1, 1, currentAttachment.Offset, true)
							}, formatter);
							builders.TextLine.ExtendLength(builders.ChordLine.CurrentLength, 0);

							//Speichere Lücke
							if (offsetBefore < builders.TextLine.CurrentLength)
								gaps.Add(new RenderGap(offsetBefore, builders.TextLine.CurrentLength));
						}
					}

					//Ist die Zeile eine Titelzeile?
					var lengthBefore = builders.TextLine.CurrentLength;
					if (builders.IsTitleLine)
					{
						//Ist die Komponente der Anfang des Titels?
						if (Content.Text?.StartsWith('[') == true)
						{
							//Ab hier wird der Titel geschrieben
							builders.IsRenderingTitle = true;

							//Schreibe die öffnende Klammer
							builders.TextLine.Append(new SheetDisplayLineSegmentTitleBracket("[")
							{
								Slice = new()
							}, formatter);

							//Ist die Komponente auch das Ende des Titels?
							if (Content.Text?.EndsWith(']') == true)
							{
								//Schreibe den Text
								var titleText = Content.GetContent(currentAttachment.Offset + 1, textLength - 2, formatter).ToString();
								builders.TextLine.Append(new SheetDisplayLineSegmentTitleText(titleText), formatter);

								//Schreibe die schließende Klammer
								builders.TextLine.Append(new SheetDisplayLineSegmentTitleBracket("]"), formatter);

								//Titel geschrieben
								builders.IsRenderingTitle = false;
							}
							else
							{
								//Schreibe den Text
								var titleText = Content.GetContent(currentAttachment.Offset + 1, textLength - 1, formatter).ToString();
								builders.TextLine.Append(new SheetDisplayLineSegmentTitleText(titleText), formatter);
							}
						}
						else if (builders.IsRenderingTitle)
						{
							//Ist die Komponente das Ende des Titels?
							if (Content.Text?.EndsWith(']') == true)
							{
								//Schreibe den Text
								var titleText = Content.GetContent(currentAttachment.Offset, textLength - 1, formatter).ToString();
								builders.TextLine.Append(new SheetDisplayLineSegmentTitleText(titleText), formatter);

								//Schreibe die schließende Klammer
								builders.TextLine.Append(new SheetDisplayLineSegmentTitleBracket("]"), formatter);

								//Titel geschrieben
								builders.IsRenderingTitle = false;
							}
							else
							{
								//Schreibe den Text
								var titleText = Content.ToString();
								builders.TextLine.Append(new SheetDisplayLineSegmentTitleText(titleText), formatter);
							}
						}
						else
						{
							//Schreibe den Text außerhalb des Titels
							var contentElement = Content.CreateElement(currentAttachment.Offset, textLength, formatter);
							builders.TextLine.Append(contentElement, formatter);
						}
					}
					else
					{
						//Schreibe den Text
						var contentElement = Content.CreateElement(currentAttachment.Offset, textLength, formatter);
						builders.TextLine.Append(contentElement, formatter);
					}

					//Schreibe ggf. das Attachment
					if (displayAttachment is not null)
					{
						//Stelle sicher, dass die Akkordzeile bisher so lang wie die Textzeile ist, um Content und Attachment zusammenzuhalten
						builders.ChordLine.ExtendLength(lengthBefore, 0);

						//Schreibe das Attachment
						lengthBefore = builders.ChordLine.CurrentLength;
						builders.ChordLine.Append(displayAttachment, formatter);
						var attachmentBounds = new RenderBounds(lengthBefore, builders.ChordLine.CurrentLength);
						setAttachmentRenderBounds(attachmentBounds);
					}
				}

				//Merke das Attachment
				currentAttachment = nextAttachment;
			}

			//Berechne Render Bounds
			contentRenderBounds = new(textStartIndex, builders.TextLine.CurrentLength) { Gaps = gaps };
			totalRenderBounds = firstAttachment.Attachment is null ? new(contentRenderBounds.StartOffset, contentRenderBounds.EndOffset)
				: new(textStartIndex, builders.CurrentLength);
		}

		private void ResetDisplayCache()
		{
			//=> displayBlocksCache = null;
		}

		internal record DisplayBlock
		{
			public SheetDisplayLineElement Content { get; }
			public SheetDisplayLineElement? Attachment { get; }

			public DisplayBlock(SheetDisplayLineElement content, SheetDisplayLineElement? attachment = null)
			{
				if (content is SheetDisplayLineText text && attachment is not null)
				{
					content = new SheetDisplayLineAnchorText(text.Text)
					{
						Targets = [attachment]
					};
				}

				Content = content;
				Attachment = attachment;
			}
		}

		private sealed class EmptyAttachmentStub : Attachment
		{
			internal override RenderBounds RenderBounds { get; private protected set; }
			public override bool IsEmpty => true;

			public EmptyAttachmentStub(int offset)
				: base(offset)
			{
				RenderBounds = new(Offset, Offset);
			}

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return null;
			}

			private static void SetRenderBounds(RenderBounds _) { }
		}
		#endregion

		#region Editing
		internal override bool TryRemoveContent(int offset, int length, ISheetFormatter? formatter)
		{
			//Speichere die Länge vor der Bearbeitung
			var lengthBefore = Content.GetLength(formatter);
			if (length == 0 || offset >= lengthBefore) return false;

			//Entferne den Inhalt
			if (!Content.RemoveContent(offset, length, formatter))
				return false;

			//Berechne die Länge nach der Bearbeitung
			ResetDisplayCache();
			var lengthAfter = Content.GetLength(formatter);

			//Wie hat sich die Länge des Inhalts verändert?
			var moved = lengthAfter - lengthBefore;

			//Entferne alle Attachments, die im Bereich liegen und verschiebe die Attachments, die dahinter liegen.
			//Behalte Attachments, die auf Offset 0 liegen
			var endOffset = offset + length;
			var index = -1;
			attachments.RemoveAll(a =>
			{
				index++;

				//Liegt das Attachment vor dem Bereich?
				if (a.Offset < offset) return false;

				//Liegt das Attachment im Bereich?
				if (a.Offset < endOffset)
				{
					//Würde sich das Attachment mit einem anderen überschneiden?
					if (moved < 0 && attachments.Skip(index + 1).Any(n => n.Offset + moved <= offset))
						return true;
					else
					{
						//Setze das Attachment an den Anfang des Bereichs
						a.SetOffset(offset);
						return false;
					}
				}

				//Verschiebe Attachments nach dem Bereich
				if (moved != 0)
					a.SetOffset(a.Offset + moved);
				return false;
			});
			ResetDisplayCache();
			return true;
		}

		internal override bool TryMerge(Component next, int offset, ISheetFormatter? formatter)
		{
			//Ist das nachfolgende Element kein VarietyComponent?
			if (next is not VarietyComponent varietyMerge)
				return false;

			//Besteht eine, aber nicht beide Komponenten nur aus Leerzeichen?
			if (Content.IsSpace != varietyMerge.Content.IsSpace)
				return false;

			//Füge Inhalt zusammen
			var lengthBefore = Content.GetLength(formatter);
			var mergeLengthBefore = varietyMerge.Content.GetLength(formatter);
			Content.MergeContents(varietyMerge.Content, offset, formatter);
			ResetDisplayCache();

			//Verschiebe alle Attachments nach dem Offset
			var lengthNow = Content.GetLength(formatter);
			var moved = lengthNow - mergeLengthBefore;
			if (moved != 0)
				foreach (var attachment in attachments)
					if (attachment.Offset >= offset)
						attachment.SetOffset(attachment.Offset + moved);

			//Füge alle Attachments zusammen und verschiebe dabei alle Attachments des eingefügten Elements
			var newAttachmentsMove = lengthBefore + moved;
			attachments.AddRange(varietyMerge.Attachments.Select(a =>
			{
				//Verschiebe das Attachment
				a.SetOffset(a.Offset + moved);
				return a;
			}));

			//Zusammenführung erfolgreich
			ResetDisplayCache();
			return true;
		}

		internal override Component SplitEnd(int offset, ISheetFormatter? formatter)
		{
			//Trenne den Inhalt
			var newEndContent = Content.SplitEnd(offset, formatter);
			var newEnd = new VarietyComponent(newEndContent);

			//Entferne alle Attachments nach der Trennung und füge sie dem neuen Ende hinzu
			attachments.RemoveAll(a =>
			{
				if (a.Offset < offset) return false;

				//Verschiebe das Attachment
				a.SetOffset(a.Offset - offset);
				newEnd.attachments.Add(a);
				return true;
			});

			ResetDisplayCache();
			return newEnd;
		}

		public void AddAttachment(Attachment attachment)
		{
			var newIndex = attachments.FindIndex(a => a.Offset > attachment.Offset);
			if (newIndex >= 0)
				attachments.Insert(newIndex, attachment);
			else
				attachments.Add(attachment);

			ResetDisplayCache();
		}

		public void AddAttachments(IEnumerable<Attachment> attachments)
		{
			this.attachments.AddRange(attachments);
			this.attachments.Sort((a1, a2) => a1.Offset - a2.Offset);
			ResetDisplayCache();
		}

		public bool RemoveAttachment(Attachment attachment)
		{
			if (!attachments.Remove(attachment))
				return false;

			ResetDisplayCache();
			return true;
		}
		#endregion

		#region Operators
		public override string? ToString() => Content.ToString();
		#endregion

		public abstract class Attachment
		{
			public int Offset { get; protected set; }

			public abstract bool IsEmpty { get; }
			internal abstract RenderBounds RenderBounds { get; private protected set; }

			protected Attachment(int offset)
			{
				if (offset < 0)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal void SetOffset(int offset)
			{
				if (offset < 0)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal abstract SheetDisplayLineElement? CreateDisplayAttachment(SheetDisplaySliceInfo sliceInfo,
				out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter);
		}

		public sealed class VarietyAttachment : Attachment
		{
			internal ComponentContent Content { get; }

			public override bool IsEmpty => Content.IsEmpty;
			internal override RenderBounds RenderBounds { get; private protected set; }

			public VarietyAttachment(int offset, string text)
				: base(offset)
			{
				Content = new(text);
			}

			public VarietyAttachment(int offset, Chord chord)
				: base(offset)
			{
				Content = new(chord);
			}

			private VarietyAttachment(int offset, ComponentContent content)
				: base(offset)
			{
				Content = content;
			}

			public static VarietyAttachment FromString(int offset, string content)
				=> new VarietyAttachment(offset, ComponentContent.FromString(content));

			internal override SheetDisplayLineElement? CreateDisplayAttachment(SheetDisplaySliceInfo sliceInfo,
				out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return Content.CreateElement(sliceInfo, formatter);
			}

			private void SetRenderBounds(RenderBounds bounds)
				=> RenderBounds = bounds;

			#region Editing
			internal bool TryRemoveContent(int offset, int length, ISheetFormatter? formatter)
			{
				//Entferne den Inhalt
				return Content.RemoveContent(offset, length, formatter);
			}

			internal void InsertContent(string content, int offset, ISheetFormatter? formatter)
			{
				//Füge Inhalt ein
				Content.MergeContents(content, offset, formatter);
			}

			internal void ReplaceContent(string content, ISheetFormatter? formatter)
			{
				//Ersetze Inhalt
				Content.SetContent(content);
			}

			internal VarietyAttachment? SplitEnd(int offset, ISheetFormatter? formatter)
			{
				//Trenne den Inhalt
				var newEndContent = Content.SplitEnd(offset, formatter);
				if (newEndContent is null) return null;

				//Erzeuge das neue Ende
				var newEnd = new VarietyAttachment(Offset + offset, newEndContent);
				return newEnd;
			}

			internal bool TryMergeContents(VarietyAttachment attachment, ISheetFormatter? formatter)
			{
				//Füge Inhalt hinzu
				Content.MergeContents(attachment.Content, int.MaxValue, formatter);
				return true;
			}
			#endregion
		}
	}
	#endregion
}
