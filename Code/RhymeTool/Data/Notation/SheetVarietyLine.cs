using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;
using Skinnix.RhymeTool.Data.Notation.Display.Caching;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetVarietyLine : SheetLine, ISheetTitleLine
{
	private readonly List<Component> components;
	private readonly ContentEditing contentEditor;
	private readonly AttachmentEditing attachmentEditor;

	private ISheetBuilderFormatter? cachedFormatter;
	private IEnumerable<SheetDisplayLine>? cachedLines;

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
		//Prüfe Cache
		if (cachedFormatter == formatter && cachedLines is not null)
			return cachedLines;

		//Erzeuge Cache
		cachedLines = CreateDisplayLinesCore(formatter).ToList();
		cachedFormatter = formatter;
		return cachedLines;
	}

	private IEnumerable<SheetDisplayLine> CreateDisplayLinesCore(ISheetBuilderFormatter? formatter)
	{
		//Erzeuge Builder
		var builders = new Component.LineBuilders(this,
			new SheetDisplayTextLine.Builder(),
			new SheetDisplayChordLine.Builder())
		{
			IsTitleLine = IsTitleLine(out _),
		};

		//Gehe durch alle Komponenten
		var componentIndex = 0;
		foreach (var component in components)
			component.BuildLines(builders, componentIndex++, formatter);

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
	private void RaiseModifiedAndInvalidateCache()
	{
		cachedLines = null;
		RaiseModified(new ModifiedEventArgs(this));
	}

	private class ContentEditing : ISheetDisplayLineEditing
	{
		public SheetVarietyLine Line { get; }
		SheetLine ISheetDisplayLineEditing.Line => Line;

		public int LineId => 0;

		public ContentEditing(SheetVarietyLine owner)
		{
			this.Line = owner;
		}

		public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetBuilderFormatter? formatter = null)
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
						if (lastComponent.TryMerge(Line.components[0], ContentOffset.FarEnd, formatter))
						{
							//Entferne die zusammengefügte Komponente
							varietyBefore.components.Remove(Line.components[0]);
						}
					}

					//Setze den Cursor an die Position, die mal das Ende der vorherigen Zeile war
					var cursorPosition = lastComponent?.DisplayRenderBounds.EndOffset ?? 0;
					return new MetalineEditResult(true, new MetalineSelectionRange(varietyBefore.ContentEditor, SimpleRange.CursorAt(cursorPosition)))
					{
						RemoveLine = true,
					};
				}

				//Wird der Zeilenumbruch am Ende entfernt?
				if (forward && Line.components.Count == 0 || context.SelectionRange.End >= Line.components[^1].DisplayRenderBounds.EndOffset)
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
						if (lastComponent.TryMerge(varietyAfter.components[0], ContentOffset.FarEnd, formatter))
						{
							//Entferne die zusammengefügte Komponente
							Line.components.Remove(varietyAfter.components[0]);
						}
					}

					//Setze den Cursor an die Position, die mal das Ende dieser Zeile war
					var cursorPosition = lastComponent?.DisplayRenderBounds.EndOffset ?? 0;
					return new MetalineEditResult(true, new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)))
					{
						RemoveLineAfter = true,
					};
				}

				//Prüfe Cursorposition relativ zu den umliegenden Komponenten
				Component? prepreviousComponent = null;
				Component? previousComponent = null;
				Component? startComponent = null;
				Component? endComponent = null;
				Component? nextComponent = null;
				foreach (var component in Line.components)
				{
					if (component.DisplayRenderBounds.EndOffset == context.SelectionRange.Start)
					{
						endComponent = component;
					}
					else if (component.DisplayRenderBounds.StartOffset == context.SelectionRange.Start)
					{
						startComponent = component;
						nextComponent = component;
						break;
					}
					else if (component.DisplayRenderBounds.StartOffset > context.SelectionRange.Start)
					{
						nextComponent = component;
						break;
					}

					prepreviousComponent = previousComponent;
					previousComponent = component;
				}

				//Lösche das Zeichen vor oder hinter dem Cursor
				if (forward)
				{
					//Steht der Cursor am Ende einer Komponente?
					if (endComponent is not null && nextComponent is not null)
					{
						//Lösche das erste Zeichen der ersten Komponente danach
						context.SelectionRange = new SimpleRange(endComponent.DisplayRenderBounds.EndOffset, nextComponent.DisplayRenderBounds.StartOffset + 1);
					}

					//Steht der Cursor mitten in einem langgestreckten Leerzeichen?
					else if (endComponent is null && previousComponent?.DisplayRenderBounds.EndOffset <= context.SelectionRange.Start && nextComponent is not null
						&& (previousComponent as VarietyComponent)?.Content.IsSpace == true)
					{
						//Lösche das erste Zeichen der ersten Komponente nach dem Leerzeichen
						context.SelectionRange = new SimpleRange(previousComponent.DisplayRenderBounds.EndOffset, nextComponent.DisplayRenderBounds.StartOffset + 1);
					}

					//Erweitere einfach die Auswahl um ein Zeichen nach rechts
					else
					{
						context.SelectionRange = new SimpleRange(context.SelectionRange.Start, context.SelectionRange.Start + 1);
					}
				}
				else
				{
					//Steht der Cursor am Anfang einer Komponente?
					if (startComponent is not null && previousComponent is not null)
					{
						//Lösche das letzte Zeichen der ersten Komponente davor
						context.SelectionRange = new SimpleRange(previousComponent.DisplayRenderBounds.EndOffset - 1, startComponent.DisplayRenderBounds.StartOffset);
					}
					
					//Steht der Cursor mitten in einem langgestreckten Leerzeichen?
					else if (startComponent is null && previousComponent?.DisplayRenderBounds.EndOffset <= context.SelectionRange.Start && prepreviousComponent is not null
						&& (previousComponent as VarietyComponent)?.Content.IsSpace == true)
					{
						//Lösche das letzte Zeichen der ersten Komponente vor dem Leerzeichen
						context.SelectionRange = new SimpleRange(previousComponent.DisplayRenderBounds.StartOffset, prepreviousComponent.DisplayRenderBounds.EndOffset - 1);
					}
					
					//Erweitere einfach die Auswahl um ein Zeichen nach links
					else
					{
						context.SelectionRange = new SimpleRange(context.SelectionRange.Start - 1, context.SelectionRange.Start);
					}
				}
			}

			return DeleteAndInsertContent(context, formatter, null, !forward);
		}

		public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetBuilderFormatter? formatter)
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
					if (component.DisplayRenderBounds.EndOffset <= context.SelectionRange.Start)
					{
						index++;
						continue;
					}

					//Liegt der Bereich in der Komponente?
					if (component.DisplayRenderBounds.StartOffset < context.SelectionRange.Start)
					{
						//Teile die Komponente
						var splitOffset = component.DisplayRenderBounds.GetContentOffset(context.SelectionRange.Start - component.DisplayRenderBounds.StartOffset);
						var end = component.SplitEnd(splitOffset.ContentOffset, formatter);

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

			return DeleteAndInsertContent(context, formatter, content, false);
		}

		private MetalineEditResult DeleteAndInsertContent(SheetDisplayLineEditingContext context, ISheetBuilderFormatter? formatter, string? content, bool deleteBackward)
		{
			//Finde alle Komponente im Bereich
			Component? leftEdge = null;
			int leftEdgeIndex = -1;
			Component? rightEdge = null;
			int rightEdgeIndex = -1;
			List<Component> fullyInside = new();
			var rangeStartsOnComponent = false;
			var rangeEndsOnComponent = false;
			var index = -1;
			foreach (var component in Line.components)
			{
				index++;

				//Beginnt die Komponente vor dem Bereich?
				if (component.DisplayRenderBounds.StartOffset < context.SelectionRange.Start)
				{
					//Die Komponente ist der linke Rand
					leftEdge = component;
					leftEdgeIndex = index;
				}

				//Liegt die Komponente komplett vor dem Bereich?
				if (component.DisplayRenderBounds.EndOffset < context.SelectionRange.Start)
					continue;

				//Beginnt die Komponente mit dem Bereich?
				if (component.DisplayRenderBounds.StartOffset == context.SelectionRange.Start)
					rangeStartsOnComponent = true;

				//Endet die Komponente mit dem Bereich?
				if (component.DisplayRenderBounds.EndOffset == context.SelectionRange.End)
					rangeEndsOnComponent = true;

				//Endet die Komponente nach dem Bereich?
				if (rightEdge is null && component.DisplayRenderBounds.EndOffset > context.SelectionRange.End)
				{
					//Die Komponente ist der rechte Rand
					rightEdge = component;
					rightEdgeIndex = index;
				}
				
				//Liegt die Komponente komplett hinter dem Bereich?
				if (component.DisplayRenderBounds.StartOffset > context.SelectionRange.End)
					break;

				//Liegt die Komponente komplett im Bereich?
				if (component != leftEdge && component != rightEdge
					&& component.DisplayRenderBounds.StartOffset >= context.SelectionRange.Start && component.DisplayRenderBounds.EndOffset <= context.SelectionRange.End)
				{
					//Die Komponente liegt im Bereich
					fullyInside.Add(component);
				}
			}

			//Prüfe auf Änderungen
			var removedAnything = fullyInside.Count != 0;
			var addedContent = false;
			var cursorPosition = context.SelectionRange.Start;
			SpecialCursorPosition? specialCursorPosition = null;

			//Sonderfall: wird ein Text unter einem Leerzeichen-Attachment eingegeben?
			List<VarietyComponent>? newContentComponents = null;
			if (content is not null && rightEdge is VarietyComponent varietyAfter && varietyAfter.Content.IsSpace && varietyAfter.Attachments.Count == 1)
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
					Line.components.Insert(rightEdgeIndex, newContentComponents[0]);

					//Füge ggf. ein Leerzeichen vor den Inhalt ein
					if (leftEdge is VarietyComponent varietyBefore && !varietyBefore.Content.IsSpace)
						Line.components.Insert(rightEdgeIndex, new VarietyComponent(" "));

					//Anfügen erfolgreich
					content = null;
					addedContent = true;

					//Setze den Cursor an das Ende des eingefügten Inhalts
					//cursorPosition = context.SelectionRange.Start + newContentComponents[0].Content.GetLength(formatter);
					specialCursorPosition = SpecialCursorPosition.Behind(newContentComponents[0]);
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
					var insideIndex = leftEdgeIndex != -1 ? leftEdgeIndex + 1
						: rightEdgeIndex != -1 ? rightEdgeIndex - 1
						: Line.components.IndexOf(fullyInside[0]);
					Line.components[insideIndex] = newContentComponents[0];

					//Anfügen erfolgreich
					content = null;
					addedContent = true;

					//Setze den Cursor an das Ende des eingefügten Inhalts
					//cursorPosition = context.SelectionRange.Start + newContentComponents[0].Content.GetLength(formatter);
					specialCursorPosition = SpecialCursorPosition.Behind(newContentComponents[0]);
				}
			}

			//Entferne Überlappung am linken Rand
			var skipTrimAfter = false;
			if (leftEdge is not null)
			{
				//Kürze die Komponente
				var leftEdgeOverlapOffset = leftEdge.DisplayRenderBounds.GetContentOffset(context.SelectionRange.Start);
				var leftEdgeOverlapLength = leftEdge.DisplayRenderBounds.GetContentOffset(context.SelectionRange.End).ContentOffset - leftEdgeOverlapOffset.ContentOffset;
				if (leftEdge.TryRemoveContent(leftEdgeOverlapOffset.ContentOffset, leftEdgeOverlapLength, formatter))
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
						if (rightEdge == leftEdge && (newContentComponents.Count > 1 || repeat))
						{
							//Teile die Randkomponente in zwei Teile
							//TODO: Wenn ein automatischer Bindestrich angezeigt wird, funktioniert das Einfügen direkt davor nicht richtig
							var newRightEdge = leftEdge.SplitEnd(leftEdgeOverlapOffset.ContentOffset, formatter);

							//Füge den neuen rechten Rand ein
							if (leftEdgeIndex == Line.components.Count - 1)
								Line.components.Add(newRightEdge);
							else
								Line.components.Insert(leftEdgeIndex + 1, newRightEdge);

							//Der rechte Rand muss jetzt nicht mehr gekürzt werden
							rightEdge = newRightEdge;
							rightEdgeIndex = leftEdgeIndex + 1;
							skipTrimAfter = true;
						}

						//Versuche die erste Komponente hinten an den linken Rand anzufügen
						var firstNewComponent = newContentComponents[0];
						if (leftEdge.TryMerge(firstNewComponent, leftEdgeOverlapOffset.ContentOffset, formatter))
						{
							//Berechne die Gesamtlänge des eingefügten Inhalts
							var lastNewComponent = newContentComponents[^1];

							//Erste Komponente hinzugefügt
							newContentComponents.RemoveAt(0);

							//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
							specialCursorPosition = null;
							if (newContentComponents.Count > 0 && rightEdge?.TryMerge(lastNewComponent, ContentOffset.Zero, formatter) == true)
							{
								//Letzte Komponente hinzugefügt
								newContentComponents.RemoveAt(newContentComponents.Count - 1);

								//Setze den Cursor an das Ende des eingefügten Inhalts im rechten Rand
								//cursorPosition = context.SelectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
								var cursorOffset = lastNewComponent.Content.GetLength(formatter);
								specialCursorPosition = SpecialCursorPosition.FromStart(rightEdge, cursorOffset, SpecialCursorVirtualPositionType.KeepLeft);
							}

							//Füge die restlichen Komponenten dazwischen ein
							if (newContentComponents.Count > 0)
							{
								if (leftEdgeIndex == Line.components.Count - 1)
									Line.components.AddRange(newContentComponents);
								else
									Line.components.InsertRange(leftEdgeIndex + 1, newContentComponents);

								//Der rechte Rand hat sich verschoben
								rightEdgeIndex += newContentComponents.Count;

								//Wurde die Cursorposition noch nicht gesetzt?
								if (specialCursorPosition is null)
								{
									//Setze den Cursor an das Ende des eingefügten Inhalts
									//cursorPosition = context.SelectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
									specialCursorPosition = SpecialCursorPosition.Behind(lastNewComponent);
								}
							}
							else
							{
								//Wurde die Cursorposition noch nicht gesetzt?
								if (specialCursorPosition is null)
								{
									//Setze den Cursor an das Ende des eingefügten Inhalts im linken Rand
									//cursorPosition = context.SelectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
									var cursorOffset = leftEdgeOverlapOffset.ContentOffset + firstNewComponent.Content.GetLength(formatter);
									specialCursorPosition = SpecialCursorPosition.FromStart(leftEdge, cursorOffset, SpecialCursorVirtualPositionType.KeepLeft);
								}
							}

							//Anfügen erfolgreich
							content = null;
							addedContent = true;
						}
						else
						{
							//Der Rand muss aufgetrennt werden
							repeat = true;
						}
					}
					while (leftEdge == rightEdge && repeat);
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
						//Versuche die Komponente zu ersetzen
						if (firstFullyInside.TryReplaceContent(newContentComponents[0].Content, formatter))
						{
							//Entferne die Komponente doch nicht
							fullyInside.RemoveAt(0);

							//Anfügen erfolgreich
							content = null;
							addedContent = true;

							//Setze den Cursor an das Ende des eingefügten Inhalts
							specialCursorPosition = SpecialCursorPosition.Behind(firstFullyInside);
						}
					}
				}
			}

			//Entferne Überlappung am rechten Rand
			if (rightEdge is not null && rightEdge != leftEdge)
			{
				//Kürze die Komponente
				var rightEdgeOverlap = rightEdge.DisplayRenderBounds.GetContentOffset(context.SelectionRange.End);
				if (!skipTrimAfter && rightEdge.TryRemoveContent(ContentOffset.Zero, rightEdgeOverlap.ContentOffset, formatter))
					removedAnything = true;

				//Gibt es einen Inhalt?
				if (content is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
					if (rightEdge.TryMerge(newContentComponents[^1], ContentOffset.Zero, formatter))
					{
						//Letzte Komponente hinzugefügt
						var lastComponent = newContentComponents[^1];
						newContentComponents.RemoveAt(newContentComponents.Count - 1);

						//Füge die restlichen Komponenten davor ein
						if (newContentComponents.Count > 0)
						{
							Line.components.InsertRange(rightEdgeIndex, newContentComponents);
						}

						//Anfügen erfolgreich
						content = null;
						addedContent = true;

						//Setze den Cursor an das Ende des eingefügten Inhalts
						//cursorPosition = context.SelectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
						var cursorOffset = lastComponent.Content.GetLength(formatter);
						specialCursorPosition = SpecialCursorPosition.FromStart(rightEdge, cursorOffset, SpecialCursorVirtualPositionType.KeepLeft);
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
				var insertIndex = leftEdgeIndex == -1 ? rightEdgeIndex : leftEdgeIndex + 1;
				if (insertIndex < 0 || insertIndex >= Line.components.Count)
					Line.components.AddRange(newContentComponents);
				else
					Line.components.InsertRange(insertIndex, newContentComponents);

				//Hinzufügen erfolgreich
				content = null;
				addedContent = true;

				//Prüfe, ob der rechte Rand auch hinzugefügt werden kann
				if (rightEdge is not null && rightEdge != leftEdge)
				{
					//Entferne die rechte Randkomponente
					Line.components.Remove(rightEdge);
					removedAnything = true;
				}

				//Setze den Cursor an das Ende des eingefügten Inhalts
				cursorPosition = context.SelectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter).Value);
				specialCursorPosition = SpecialCursorPosition.Behind(newContentComponents[^1]);
			}

			//Wurde die Cursorposition immer noch nicht gesetzt?
			if (specialCursorPosition is null)
			{
				//Nur fullyInside wurde entfernt und nichts hinzugefügt, setze den Cursor ans Ende des Bereichs
				if (removedAnything && !addedContent)
				{
					//Setze den Cursor an das Ende des Bereichs
					if (rightEdge is not null)
						specialCursorPosition = SpecialCursorPosition.Before(rightEdge, deleteBackward ? SpecialCursorVirtualPositionType.KeepRight : SpecialCursorVirtualPositionType.KeepLeft);
					else if (leftEdge is not null)
						specialCursorPosition = SpecialCursorPosition.Behind(leftEdge, deleteBackward ? SpecialCursorVirtualPositionType.KeepRight : SpecialCursorVirtualPositionType.KeepLeft);
				}

				//Der Rand wurde getrimmt
				else if (removedAnything)
				{

				}
			}

			//Prüfe, ob der linke Rand entfernt werden kann
			if (leftEdge is not null && leftEdge.IsEmpty)
			{
				//Entferne die linke Randkomponente
				Line.components.Remove(leftEdge);
				removedAnything = true;
			}

			//Prüfe, ob der rechte Rand entfernt werden kann
			if (rightEdge is not null && rightEdge != leftEdge && rightEdge.IsEmpty)
			{
				//Entferne die rechte Randkomponente
				Line.components.Remove(rightEdge);
				removedAnything = true;
			}

			//Prüfe, ob Komponenten zusammengefügt werden können. Beginne mit der Komponente links vom linken Rand
			var stopCombining = false;
			for (var current = Math.Max(leftEdgeIndex - 1, 0); current < Line.components.Count - 1 && !stopCombining; current++)
			{
				//Prüfe diese und die folgende Komponente
				var component = Line.components[current];
				var nextComponent = Line.components[current + 1];

				//Soll der Cursor auf eine dieser Komponenten gesetzt werden?
				ContentOffset? componentLength = null;
				if ((specialCursorPosition?.Component, specialCursorPosition?.Type) == (component, SpecialCursorPositionType.Behind)
					|| (specialCursorPosition?.Component, specialCursorPosition?.Type) == (nextComponent, SpecialCursorPositionType.Before))
				{
					//Speichere die Länge der Komponente
					componentLength = (component as VarietyComponent)?.Content.GetLength(formatter);
				}

				//Kann die Komponente mit der darauf folgenden zusammengeführt werden?
				if (component.TryMerge(nextComponent, ContentOffset.FarEnd, formatter))
				{
					//Falls der Cursor hinter diese Komponente gesetzt werden soll, passe die Cursorposition an
					if (componentLength.HasValue)
					{
						//Passe die Cursorposition an, damit der Cursor immer noch auf der gleichen Position steht
						specialCursorPosition = SpecialCursorPosition.FromStart(component, componentLength.Value, specialCursorPosition!.Value.OffsetType);
					}

					//Das sollte nicht passieren
					//Debugger.Break();

					//Entferne die folgende Komponente
					Line.components.RemoveAt(current + 1);
					removedAnything = true;
					current--;
				}

				//Höre nach dem rechten Rand auf
				if (nextComponent == rightEdge)
					stopCombining = true;
			}

			//Nicht erfolgreich?
			if (!removedAnything && !addedContent)
				return MetalineEditResult.Fail;

			//Zeile bearbeitet
			Line.RaiseModifiedAndInvalidateCache();
			Line.cachedLines = null;

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

			//Berechne Cursorposition
			if (specialCursorPosition is not null)
			{
				//Erzeuge Displayelemente, damit die Cursorposition berechnet werden kann
				Line.CreateDisplayLines(formatter);

				//Berechne Cursorposition
				cursorPosition = specialCursorPosition.Value.Calculate(Line, formatter);
			}

			//Setze Cursorposition
			return this.CreateSuccessEditResult(SimpleRange.CursorAt(cursorPosition));
		}

		private readonly record struct SpecialCursorPosition(SpecialCursorPositionType Type, Component Component,
			ContentOffset ContentOffset = default, SpecialCursorVirtualPositionType OffsetType = SpecialCursorVirtualPositionType.KeepLeft)
		{
			public static SpecialCursorPosition Before(Component component, SpecialCursorVirtualPositionType offsetType = SpecialCursorVirtualPositionType.KeepLeft)
				=> new(SpecialCursorPositionType.Before, component, OffsetType: offsetType);
			public static SpecialCursorPosition Behind(Component component, SpecialCursorVirtualPositionType offsetType = SpecialCursorVirtualPositionType.KeepLeft)
				=> new(SpecialCursorPositionType.Behind, component, OffsetType: offsetType);

			public static SpecialCursorPosition FromStart(Component component, ContentOffset contentOffset, SpecialCursorVirtualPositionType offsetType)
				=> new(SpecialCursorPositionType.Before, component, contentOffset, offsetType);

			public int Calculate(SheetVarietyLine line, ISheetBuilderFormatter? formatter)
			{
				//Finde den Anzeige-Offset zum Inhaltsoffset
				var displayOffset = 0;
				if (ContentOffset != ContentOffset.Zero)
				{
					//Berechne den Anzeige-Offset
					displayOffset = Component.DisplayRenderBounds.GetDisplayOffset(ContentOffset, keepRight: OffsetType == SpecialCursorVirtualPositionType.KeepRight)
						- Component.DisplayRenderBounds.StartOffset;
				}

				return Type switch
				{
					SpecialCursorPositionType.Before => Component.DisplayRenderBounds.StartOffset + displayOffset,
					SpecialCursorPositionType.Behind => Component.DisplayRenderBounds.EndOffset + displayOffset,
					_ => Component.DisplayRenderBounds.StartOffset + displayOffset,
				};
			}
		}

		private enum SpecialCursorPositionType
		{
			Before,
			Behind,
		}

		private enum SpecialCursorVirtualPositionType
		{
			KeepLeft,
			KeepRight,
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

		public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetBuilderFormatter? formatter = null)
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
				Line.RaiseModifiedAndInvalidateCache();
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(context.SelectionRange.Start));
			}

			//Finde das nächste Attachment und verschiebe es nach links
			return FindAndMoveNextAttachment(context.SelectionRange.Start, new(-context.SelectionRange.Length), formatter);
		}

		public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetBuilderFormatter? formatter)
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
				var moveOffset = new ContentOffset(content.Length - context.SelectionRange.Length);
				return FindAndMoveNextAttachment(context.SelectionRange.Start, moveOffset, formatter);
			}

			//Ist der Bereich eine Selektion?
			if (context.SelectionRange.Length > 0)
				return MetalineEditResult.Fail;

			//Finde die Komponente, in der der Inhalt eingefügt werden soll
			var component = Line.components.OfType<VarietyComponent>()
				.FirstOrDefault(c => c.DisplayRenderBounds.StartOffset <= context.SelectionRange.Start && c.DisplayRenderBounds.EndOffset > context.SelectionRange.End);
			if (component is null)
				return MetalineEditResult.Fail;

			//Liegt ein Attachment direkt vor oder hinter der Position?
			var before = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().LastOrDefault(a => a.RenderBounds.EndOffset == context.SelectionRange.Start - 1);
			var after = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().FirstOrDefault(a => a.RenderBounds.StartOffset == context.SelectionRange.End);
			if (before is not null)
			{
				//Füge den Inhalt ans Ende des Attachments an
				before.InsertContent(content, ContentOffset.FarEnd, formatter);

				//Erzeuge die Displayelemente der Zeile neu
				Line.RaiseModifiedAndInvalidateCache();
				Line.CreateDisplayLines(formatter);

				//Setze den Cursor hinter das Attachment
				var cursorPosition = before.RenderBounds.EndOffset;
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(cursorPosition));
			}
			else if (after is not null)
			{
				//Füge den Inhalt an den Anfang des Attachments an
				after.InsertContent(content, ContentOffset.Zero, formatter);

				//Erzeuge die Displayelemente der Zeile neu
				Line.RaiseModifiedAndInvalidateCache();
				Line.CreateDisplayLines(formatter);

				//Setze den Cursor hinter den eingefügten Text
				var cursorPosition = after.RenderBounds.StartOffset + content.Length;
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(cursorPosition));
			}
			else
			{
				//Erzeuge ein neues Attachment
				var componentOffset = component.DisplayRenderBounds.GetContentOffset(context.SelectionRange.Start);
				var newAttachment = VarietyComponent.VarietyAttachment.FromString(componentOffset.ContentOffset, content);
				component.AddAttachment(newAttachment);

				//Erzeuge die Displayelemente der Zeile neu
				Line.RaiseModifiedAndInvalidateCache();
				Line.CreateDisplayLines(formatter);

				//Setze den Cursor hinter das eingefügte Attachment
				var cursorPosition = newAttachment.RenderBounds.EndOffset;
				return this.CreateSuccessEditResult(SimpleRange.CursorAt(cursorPosition));
			}
		}

		private IEnumerable<(VarietyComponent Component, VarietyComponent.VarietyAttachment Attachment)> FindAttachmentsInRange(SimpleRange range, ISheetFormatter? formatter)
		{
			//Finde alle Attachments im Bereich
			foreach (var component in Line.components)
			{
				//Liegt die Komponente vor dem Bereich?
				if (component.TotalRenderBounds.EndOffset < range.Start)
					continue;

				//Liegt die Komponente hinter dem Bereich?
				if (component.TotalRenderBounds.StartOffset > range.End)
					break;

				//Ist die Komponente keine VarietyComponent?
				if (component is not VarietyComponent varietyComponent)
					continue;

				//Gehe durch alle Attachments
				foreach (var attachment in varietyComponent.Attachments)
				{
					//Liegt das Attachment vor dem Bereich?
					if (attachment.RenderBounds.EndOffset < range.Start)
						continue;

					//Liegt das Attachment hinter dem Bereich?
					if (attachment.RenderBounds.StartOffset > range.End)
						break;

					//Ist das Attachment kein VarietyComponent?
					if (attachment is not VarietyComponent.VarietyAttachment varietyAttachment)
						continue;

					yield return (varietyComponent, varietyAttachment);
				}
			}
		}

		private MetalineEditResult EditAttachment(VarietyComponent component, VarietyComponent.VarietyAttachment attachment, SimpleRange selectionRange, string? content, ISheetBuilderFormatter? formatter)
		{
			//Kürze das Attachment
			var attachmentOffset = attachment.RenderBounds.GetContentOffset(selectionRange.Start);
			var selectionContentLength = attachment.RenderBounds.GetContentOffset(selectionRange.End) - attachmentOffset;
			var attachmentDisplayLength = attachment.RenderBounds.Length;
			var removedLength = 0;
			if (selectionRange.Length > 0)
			{
				if (attachment.TryRemoveContent(attachmentOffset, selectionContentLength, formatter))
					removedLength = selectionRange.Length;
				else if (content is null)
					return MetalineEditResult.Fail;
			}
			
			//Füge den Inhalt ein
			if (content is not null)
				attachment.InsertContent(content, attachmentOffset, formatter);

			//Entferne das Attachment, falls es jetzt leer ist
			if (attachment.IsEmpty)
				component.RemoveAttachment(attachment);

			//Erzeuge die Displayelemente der Zeile neu
			Line.RaiseModifiedAndInvalidateCache();
			Line.CreateDisplayLines(formatter);

			//TODO: Cursorposition beim Bearbeiten von Attachments optimieren
			//Berechne die Cursorposition
			var cursorPosition = selectionRange.Start + attachment.RenderBounds.Length - attachmentDisplayLength + removedLength;

			//Fertig
			return this.CreateSuccessEditResult(SimpleRange.CursorAt(cursorPosition));
		}

		private MetalineEditResult FindAndMoveNextAttachment(int startOffset, ContentOffset moveOffset, ISheetFormatter? formatter)
		{
			//Finde das nächste Attachment
			var nextAttachment = FindAttachmentsInRange(new SimpleRange(startOffset, int.MaxValue), formatter).FirstOrDefault();
			if (nextAttachment.Attachment is null)
				return MetalineEditResult.Fail;

			//Berechne den neuen Offset
			var newOffset = nextAttachment.Attachment.Offset + moveOffset;
			if (newOffset < ContentOffset.Zero)
				newOffset = ContentOffset.Zero;
			else
			{
				var contentLength = nextAttachment.Component.Content.GetLength(formatter);
				if (newOffset >= contentLength)
					newOffset = contentLength - new ContentOffset(1);
			}

			//Hat sich der Offset nicht verändert?
			if (newOffset == nextAttachment.Attachment.Offset)
				return MetalineEditResult.Fail;

			//Verschiebe das Attachment
			nextAttachment.Attachment.SetOffset(newOffset);

			//Zeile bearbeitet
			Line.RaiseModifiedAndInvalidateCache();
			return this.CreateSuccessEditResult(SimpleRange.CursorAt(moveOffset < ContentOffset.Zero ? startOffset : startOffset + moveOffset.Value));
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

	internal readonly record struct MergeResult(int LengthBefore)
	{
		public int MergeLengthBefore { get; init; }
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

		public static ComponentContent CreateSpace(ContentOffset length, SpecialContentType allowedTypes = SpecialContentType.Chord)
			=> new ComponentContent(new string(' ', length.Value), allowedTypes);

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

		public SheetDisplayLineElement CreateElement(SheetDisplaySliceInfo sliceId, ContentOffset offset, ContentOffset length, ISheetFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			if (offset == ContentOffset.Zero && length.Value >= textContent.Length)
				return CreateElement(sliceId, formatter);

			//Bilde Substring
			var subContent = FromString(textContent.Substring(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value)), CurrentType);
			return subContent.CreateElement(sliceId, formatter);
		}

		public ContentOffset GetLength(ISheetFormatter? formatter)
			=> new(Text?.Length
			?? Chord?.ToString(formatter).Length
			?? 0);

		public ComponentContent GetContent(ContentOffset offset, ContentOffset length, ISheetFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			if (offset == ContentOffset.Zero && length.Value >= textContent.Length)
				return this;

			//Bilde Substring
			return FromString(textContent.Substring(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value)), CurrentType);
		}

		internal void ReplaceContent(ComponentContent newContent)
		{
			if (newContent.Chord is not null && AllowedTypes.HasFlag(SpecialContentType.Chord))
			{
				//Der Inhalt ist ein Akkord
				Chord = newContent.Chord;
				Text = null;
			}
			else if (newContent.Text is not null)
			{
				//Der Inhalt ist ein Text
				Text = newContent.Text;
				Chord = null;
			}
			else
			{
				//Finde Inhaltstyp automatisch
				SetContent(newContent.ToString());
			}
		}

		internal bool RemoveContent(ContentOffset offset, ContentOffset length, ISheetFormatter? formatter)
		{
			if (offset < ContentOffset.Zero)
			{
				length -= offset;
				offset = ContentOffset.Zero;
			}

			if (length <= ContentOffset.Zero)
				return false;

			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter);
			if (textContent is null) return false;

			//Kürze den Textinhalt
			if (offset.Value >= textContent.Length) return false;
			var newContent = textContent.Remove(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value));
			if (newContent == textContent) return false;

			//Setze den neuen Inhalt
			SetContent(newContent);
			return true;
		}

		internal MergeResult AppendContent(string content, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter);

			//Füge den Textinhalt hinzu
			var newContent = textContent + content;
			SetContent(newContent);

			//Ergebnis
			return new(textContent?.Length ?? 0)
			{
				MergeLengthBefore = content.Length
			};
		}

		internal MergeResult MergeContents(string content, ContentOffset offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset.Value, textContent.Length);
			var newContent = textContent[0..stringOffset] + content;
			if (offset.Value < textContent.Length)
				newContent += textContent[stringOffset..];
			SetContent(newContent);

			//Ergebnis
			return new(textContent.Length)
			{
				MergeLengthBefore = content.Length
			};
		}

		internal MergeResult MergeContents(ComponentContent content, ContentOffset offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			var afterTextContent = content.Text ?? content.Chord?.ToString(formatter);

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset.Value, textContent.Length);
			var newContent = textContent[0..stringOffset] + afterTextContent;
			if (offset.Value < textContent.Length)
				newContent += textContent[stringOffset..];
			SetContent(newContent);

			//Ergebnis
			return new(textContent.Length)
			{
				MergeLengthBefore = afterTextContent?.Length ?? 0
			};
		}

		internal ComponentContent SplitEnd(ContentOffset offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;

			//Trenne den Textinhalt auf
			var newContent = textContent[..offset.Value];
			var newEndContent = textContent[offset.Value..];

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

		public ContentOffset GetContentOffset(int displayOffset)
			=> new(displayOffset - StartOffset);
	}

	internal record DisplayRenderBounds(int StartOffset, int EndOffset, IReadOnlyList<SheetDisplayLineElement> DisplayElements)
	{
		public static readonly DisplayRenderBounds Empty = new(0, 0, []);

		public int Length => EndOffset - StartOffset;

		public VirtualContentOffset GetContentOffset(int displayOffset)
		{
			//Finde das Element, in dem der Offset liegt
			var display = DisplayElements.LastOrDefault(d => d.Slice.HasValue && d.DisplayOffset <= displayOffset);
			if (display is null)
				return new(ContentOffset.Zero, displayOffset);

			//Ist das Element virtuell?
			var offsetFromStart = displayOffset - display.DisplayOffset;
			if (display.Slice!.Value.IsVirtual)
				return new(display.Slice.Value.ContentOffset, offsetFromStart);

			//Berechne den tatsächlichen ContentOffset
			var contentOffset = display.Slice.Value.ContentOffset + new ContentOffset(offsetFromStart);
			return new(contentOffset, 0);
		}

		public int GetDisplayOffset(ContentOffset contentOffset, bool keepRight = false)
		{
			if (!keepRight)
			{
				//Finde das letzte nicht-virtuelle Element, das vor dem Offset liegt
				var display = DisplayElements.LastOrDefault(d => !d.Slice!.Value.IsVirtual && d.Slice!.Value.ContentOffset <= contentOffset);

				//Fallback, falls kein Element gefunden wurde
				if (display is null)
				{
					if (contentOffset == ContentOffset.Zero)
						return StartOffset;
					else
						return EndOffset;
				}

				//Berechne den Offset
				return display.DisplayOffset + contentOffset.Value - display.Slice!.Value.ContentOffset.Value;
			}
			else
			{
				//Finde das letzte Element, das vor dem Offset liegt
				var display = DisplayElements
					.Select((d, i) => (Display: d, Index: i))
					.LastOrDefault(d => d.Display.Slice!.Value.ContentOffset <= contentOffset);

				//Fallback, falls kein Element gefunden wurde
				if (display.Display is null)
				{
					if (contentOffset == ContentOffset.Zero)
						return StartOffset;
					else
						return EndOffset;
				}

				//Ist das Element virtuell und entspricht genau dem Offset?
				if (display.Display.Slice!.Value.IsVirtual && display.Display.Slice!.Value.ContentOffset == contentOffset)
				{
					//Gehe zum Anfang des nächsten Elements
					if (display.Index + 1 < DisplayElements.Count)
						return DisplayElements[display.Index + 1].DisplayOffset;
					else
						return EndOffset;
				}

				//Berechne den Offset
				return display.Display.DisplayOffset + contentOffset.Value - display.Display.Slice!.Value.ContentOffset.Value;
			}
		}
	}

	public abstract class Component
	{
		public abstract bool IsEmpty { get; }

		internal abstract DisplayRenderBounds DisplayRenderBounds { get; }
		internal abstract RenderBounds TotalRenderBounds { get; }
		internal abstract IReadOnlyList<SheetDisplayLineElement> ContentElements { get; }

		internal abstract void BuildLines(LineBuilders builders, int componentIndex, ISheetBuilderFormatter? formatter);

		internal abstract bool TryRemoveContent(ContentOffset offet, ContentOffset length, ISheetFormatter? formatter);
		internal abstract bool TryReplaceContent(ComponentContent newContent, ISheetFormatter? formatter);
		internal abstract bool TryMerge(Component next, ContentOffset offset, ISheetFormatter? formatter);
		internal abstract Component SplitEnd(ContentOffset offset, ISheetFormatter? formatter);

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

		private DisplayRenderBounds displayRenderBounds = DisplayRenderBounds.Empty;
		internal override DisplayRenderBounds DisplayRenderBounds => displayRenderBounds;

		private RenderBounds totalRenderBounds = RenderBounds.Empty;
		internal override RenderBounds TotalRenderBounds => totalRenderBounds;

		private List<SheetDisplayLineElement> contentElements = new();
		internal override IReadOnlyList<SheetDisplayLineElement> ContentElements => contentElements;

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

		public static VarietyComponent CreateSpace(ContentOffset length, SpecialContentType allowedTypes = SpecialContentType.None)
			=> new VarietyComponent(ComponentContent.CreateSpace(length, allowedTypes));

		#region Display
		internal override void BuildLines(LineBuilders builders, int componentIndex, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Finde das erste Attachment
			(Attachment Attachment, SheetDisplayLineElement Display) firstAttachment = attachments
				.Select((a, i) => (Attachment: a, Display: a.CreateDisplayAttachment(new(i, a.Offset), out _, formatter)))
				.FirstOrDefault(a => a.Display is not null)!;
			if (firstAttachment.Attachment is not null)
			{
				//An welchen Offset soll das Attachment geschrieben werden?
				var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, firstAttachment.Display)
					?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
				var targetOffset = builders.ChordLine.CurrentLength + spaceBefore;

				//Wie viel mehr Platz wird auf der Akkordzeile benötigt, damit das Attachment passt?
				var difference = targetOffset - builders.TextLine.CurrentLength - firstAttachment.Attachment.Offset.Value;

				//Verlängere die Textzeile auch um diese Differenz
				builders.TextLine.ExtendLength(0, difference);
			}

			//Speichere aktuelle Textlänge für Render Bounds
			var textStartIndex = builders.TextLine.CurrentLength;
			var chordStartIndex = builders.ChordLine.CurrentLength;

			//Trenne den Text an Attachments
			var contentDisplayElements = new List<SheetDisplayLineElement>();
			foreach (var block in GetDisplayColumns(componentIndex, formatter))
			{
				//Wird ein Attachment geschrieben?
				if (block.Attachment is not null)
				{
					//Lasse Platz vor dem Attachment
					var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, block.Attachment)
						?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
					builders.ChordLine.ExtendLength(0, spaceBefore);

					//Stelle sicher, dass die Textzeile bisher so lang wie die Akkordzeile ist, um Content und Attachment zusammenzuhalten
					var textLineGap = builders.ChordLine.CurrentLength - builders.TextLine.CurrentLength;

					//Berechne die Lücke
					if (textLineGap > 0)
					{
						//Schreibe ggf. einen Bindestrich
						var offsetBefore = builders.TextLine.CurrentLength;
						var hyphen = new SheetDisplayLineHyphen(textLineGap)
						{
							Slice = block.Content.Slice!.Value with
							{
								IsVirtual = true,
							}
						};
						contentDisplayElements.Add(hyphen);
						builders.TextLine.Append(hyphen, formatter);
					}
				}

				//Merke die Länge der Textzeile
				var textLineLengthBefore = builders.TextLine.CurrentLength;

				//Ist die Zeile eine Titelzeile?
				var isTitle = false;
				foreach (var titleElement in TrySplitTitleElement(block.Content, builders.IsRenderingTitle))
				{
					isTitle = true;

					//An Klammern beginnt/endet der Titel
					if (titleElement is SheetDisplayLineSegmentTitleBracket titleBracket)
						builders.IsRenderingTitle = !builders.IsRenderingTitle;

					//Schreibe Titelelemente
					builders.TextLine.Append(titleElement, formatter);
					contentDisplayElements.Add(titleElement);
				}

				//Kein Titelelement?
				if (!isTitle)
				{
					//Schreibe Inhalt
					builders.TextLine.Append(block.Content, formatter);
					contentDisplayElements.Add(block.Content);
				}

				//Schreibe ggf. das Attachment
				if (block.Attachment is not null)
				{
					//Stelle sicher, dass die Akkordzeile bisher so lang wie die Textzeile ist, um Content und Attachment zusammenzuhalten
					builders.ChordLine.ExtendLength(textLineLengthBefore, 0);

					//Schreibe das Attachment
					textLineLengthBefore = builders.ChordLine.CurrentLength;
					builders.ChordLine.Append(block.Attachment, formatter);
					var attachmentBounds = new RenderBounds(textLineLengthBefore, builders.ChordLine.CurrentLength);
					block.SetAttachmentRenderBounds?.Invoke(attachmentBounds);
				}
			}

			//Berechne Render Bounds
			displayRenderBounds = new(textStartIndex, builders.TextLine.CurrentLength, contentDisplayElements);
			totalRenderBounds = firstAttachment.Attachment is null ? new(displayRenderBounds.StartOffset, displayRenderBounds.EndOffset)
				: new(textStartIndex, builders.CurrentLength);
			contentElements = contentDisplayElements;
		}

		private IEnumerable<(SheetDisplayLineElement Content, SheetDisplayLineElement? Attachment, Action<RenderBounds>? SetAttachmentRenderBounds)>
			GetDisplayColumns(int componentIndex, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Trenne den Text an Attachments
			Attachment? currentAttachment = null;
			var nextAttachmentIndex = -2;
			var currentAttachmentIndex = 0;
			foreach (var nextAttachment in attachments.Prepend(new EmptyAttachmentStub(ContentOffset.Zero)).Append(new EmptyAttachmentStub(contentLength)))
			{
				nextAttachmentIndex++;

				//Merke das erste Attachment
				if (currentAttachment is null)
				{
					currentAttachment = nextAttachment;
					currentAttachmentIndex = nextAttachmentIndex;
					continue;
				}

				//Berechne Textlänge
				var textLength = nextAttachment.Offset - currentAttachment.Offset;
				if (textLength <= ContentOffset.Zero)
				{
					//Nichts schreiben
					currentAttachment = nextAttachment;
					currentAttachmentIndex = nextAttachmentIndex;
					continue;
				}

				//Erzeuge das Attachment
				var currentOffset = currentAttachment.Offset;
				var slice = new SheetDisplaySliceInfo(componentIndex, currentOffset, false);
				var displayAttachment = currentAttachment.CreateDisplayAttachment(slice,
					out var setAttachmentRenderBounds, formatter);

				//Erzeuge den Inhalt
				var displayContent = Content.CreateElement(slice, currentAttachment.Offset, textLength, formatter);
				yield return (displayContent, displayAttachment, setAttachmentRenderBounds);

				//Nächstes Attachment
				currentAttachment = nextAttachment;
				currentAttachmentIndex = nextAttachmentIndex;
			}
		}

		private static IEnumerable<SheetDisplayLineElement> TrySplitTitleElement(SheetDisplayLineElement element, bool isCurrentlyWritingTitle)
		{
			if (element is not SheetDisplayLineText textElement)
				yield break;

			//Ist der Inhalt der Anfang eines Titels?
			var text = textElement.Text;
			var sliceIndex = 0;
			if (!isCurrentlyWritingTitle && text.StartsWith('['))
			{
				//Trenne öffnende Klammer
				yield return new SheetDisplayLineSegmentTitleBracket("[")
				{
					Slice = textElement.Slice,
				};
				sliceIndex++;
				text = text[1..];
				isCurrentlyWritingTitle = true;
			}

			//Ist die Komponente auch das Ende des Titels?
			if (text.EndsWith(']'))
			{
				//Trenne den Text
				var titleText = text[..^1];
				yield return new SheetDisplayLineSegmentTitleText(titleText)
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = new(1),
					},
				};

				//Trenne schließende Klammer
				yield return new SheetDisplayLineSegmentTitleBracket("]")
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = new(textElement.Text.Length - 1),
					},
				};
			}
			else if (isCurrentlyWritingTitle)
			{
				//Trenne den Text
				var titleText = text;
				yield return new SheetDisplayLineSegmentTitleText(titleText)
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = new(1),
					},
				};
			}
			else
			{
				//Nur Text
				yield break;
			}
		}

		private void ResetDisplayCache()
		{
			//=> displayBlocksCache = null;
		}

		private sealed class EmptyAttachmentStub : Attachment
		{
			internal override RenderBounds RenderBounds
			{
				get => throw new NotSupportedException();
				private protected set => throw new NotSupportedException();
			}

			public override bool IsEmpty => true;

			public EmptyAttachmentStub(ContentOffset offset)
				: base(offset)
			{ }

			internal override SheetDisplayLineElement? CreateDisplayAttachment(SheetDisplaySliceInfo sliceInfo, out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return null;
			}

			private static void SetRenderBounds(RenderBounds _) { }
		}
		#endregion

		#region Editing
		internal override bool TryReplaceContent(ComponentContent newContent, ISheetFormatter? formatter)
		{
			//Passt der Inhaltstyp nicht?
			if (newContent.Chord is not null && (Content.AllowedTypes & SpecialContentType.Chord) == 0)
				return false;
			if (newContent.IsSpace != Content.IsSpace)
				return false;

			//Ersetze den Inhalt
			Content.ReplaceContent(newContent);

			//Entferne alle Attachments, die außerhalb des Inhalts liegen
			var lengthAfter = Content.GetLength(formatter);
			attachments.RemoveAll(a => a.Offset >= lengthAfter);
			return true;
		}

		internal override bool TryRemoveContent(ContentOffset offset, ContentOffset length, ISheetFormatter? formatter)
		{
			//Speichere die Länge vor der Bearbeitung
			var lengthBefore = Content.GetLength(formatter);
			if (length == ContentOffset.Zero || offset >= lengthBefore) return false;

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
					if (moved < ContentOffset.Zero && attachments.Skip(index + 1).Any(n => n.Offset + moved <= offset))
						return true;
					else
					{
						//Setze das Attachment an den Anfang des Bereichs
						a.SetOffset(offset);
						return false;
					}
				}

				//Verschiebe Attachments nach dem Bereich
				if (moved != ContentOffset.Zero)
					a.SetOffset(a.Offset + moved);
				return false;
			});
			ResetDisplayCache();
			return true;
		}

		internal override bool TryMerge(Component next, ContentOffset offset, ISheetFormatter? formatter)
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
			var moved = lengthNow - lengthBefore;
			if (moved != ContentOffset.Zero)
				foreach (var attachment in attachments)
					if (attachment.Offset >= offset)
						attachment.SetOffset(attachment.Offset + moved);

			//Füge alle Attachments zusammen und verschiebe dabei alle Attachments des eingefügten Elements
			var newAttachmentsMove = lengthNow - mergeLengthBefore;
			attachments.AddRange(varietyMerge.Attachments.Select(a =>
			{
				//Verschiebe das Attachment
				a.SetOffset(a.Offset + newAttachmentsMove);
				return a;
			}));

			//Zusammenführung erfolgreich
			ResetDisplayCache();
			return true;
		}

		internal override Component SplitEnd(ContentOffset offset, ISheetFormatter? formatter)
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
			this.attachments.Sort((a1, a2) => a1.Offset.Value - a2.Offset.Value);
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
			public ContentOffset Offset { get; protected set; }

			public abstract bool IsEmpty { get; }
			internal abstract RenderBounds RenderBounds { get; private protected set; }

			protected Attachment(ContentOffset offset)
			{
				if (offset < ContentOffset.Zero)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal void SetOffset(ContentOffset offset)
			{
				if (offset < ContentOffset.Zero)
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

			public VarietyAttachment(ContentOffset offset, string text)
				: base(offset)
			{
				Content = new(text);
			}

			public VarietyAttachment(ContentOffset offset, Chord chord)
				: base(offset)
			{
				Content = new(chord);
			}

			private VarietyAttachment(ContentOffset offset, ComponentContent content)
				: base(offset)
			{
				Content = content;
			}

			public static VarietyAttachment FromString(ContentOffset offset, string content)
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
			internal bool TryRemoveContent(ContentOffset offset, ContentOffset length, ISheetFormatter? formatter)
			{
				//Entferne den Inhalt
				return Content.RemoveContent(offset, length, formatter);
			}

			internal void InsertContent(string content, ContentOffset offset, ISheetFormatter? formatter)
			{
				//Füge Inhalt ein
				Content.MergeContents(content, offset, formatter);
			}

			internal void ReplaceContent(string content, ISheetFormatter? formatter)
			{
				//Ersetze Inhalt
				Content.SetContent(content);
			}

			internal VarietyAttachment? SplitEnd(ContentOffset offset, ISheetFormatter? formatter)
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
				Content.MergeContents(attachment.Content, ContentOffset.FarEnd, formatter);
				return true;
			}
			#endregion
		}
	}
	#endregion
}
