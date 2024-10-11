using System;
using System.Buffers;
using System.Collections;
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

public class SheetVarietyLine : SheetLine, ISelectableSheetLine, ISheetTitleLine
{
	public static SheetLineType LineType { get; } = SheetLineType.Create<SheetVarietyLine>("Text");

	public event EventHandler? IsTitleLineChanged;

	private readonly ComponentCollection components;
	private readonly ContentEditing contentEditor;
	private readonly AttachmentEditing attachmentEditor;

	private ISheetBuilderFormatter? cachedFormatter;
	private IEnumerable<SheetDisplayLine>? cachedLines;
	private string? cachedTitle;

	public ISheetDisplayLineEditing ContentEditor => contentEditor;
	public ISheetDisplayLineEditing AttachmentEditor => attachmentEditor;

	public int TextLineId => contentEditor.LineId;
	public int AttachmentLineId => attachmentEditor.LineId;

	public override bool IsEmpty => components.Count == 0;

	public SheetVarietyLine()
		: base(LineType)
	{
		components = new(this);

		contentEditor = new(this);
		attachmentEditor = new(this);
	}

	public SheetVarietyLine(IEnumerable<Component> components)
		: base(LineType)
	{
		this.components = new(this, components);

		contentEditor = new(this);
		attachmentEditor = new(this);
	}

	public override IEnumerable<SheetLineConversion> GetPossibleConversions(ISheetBuilderFormatter? formatter = null)
	{
		if (!IsEmpty)
			return [];

		return [SheetLineConversion.Simple<SheetTabLine>.Instance];
	}

	#region Display
	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
	{
		//Prüfe Cache
		if (cachedFormatter == formatter && cachedLines is not null)
			return cachedLines;

		//Erzeuge Cache
		cachedLines = CreateDisplayLinesCore(formatter).ToList();

		//Speichere Formatter
		if (cachedFormatter is IModifiable modifiableCachedFormatter)
			modifiableCachedFormatter.Modified -= OnCachedFormatterModified;
		cachedFormatter = formatter;
		if (cachedFormatter is IModifiable modifiableFormatter)
			modifiableFormatter.Modified += OnCachedFormatterModified;
		return cachedLines;
	}

	private void OnCachedFormatterModified(object? sender, ModifiedEventArgs e) => InvalidateCache();

	private void InvalidateCache()
	{
		cachedLines = null;
		if (cachedFormatter is IModifiable modifiableCachedFormatter)
			modifiableCachedFormatter.Modified -= OnCachedFormatterModified;
		cachedFormatter = null;
	}

	private void RaiseModifiedAndInvalidateCache()
	{
		InvalidateCache();

		//Hat sich der Titel oder Titelstatus geändert?
		IsTitleLine(out var title);
		if (title != cachedTitle)
		{
			cachedTitle = title;
			IsTitleLineChanged?.Invoke(this, EventArgs.Empty);
		}
		
		RaiseModified(new ModifiedEventArgs(this));
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

		//Nachbearbeitung der Zeilen
		formatter?.AfterPopulateLine(this, builders.TextLine, builders.AllLines);
		formatter?.AfterPopulateLine(this, builders.ChordLine, builders.AllLines);

		//Zeige Akkordzeile
		if (formatter?.ShowLine(this, builders.ChordLine) != false)
			yield return builders.ChordLine.CreateDisplayLine(1, attachmentEditor);

		//Zeige Textzeile
		if (formatter?.ShowLine(this, builders.TextLine) != false)
			yield return builders.TextLine.CreateDisplayLine(0, contentEditor);
	}
	#endregion

	#region Title
	public bool IsTitleLine(out string? title)
		=> CheckTitleLine(out title, out _, out _);

	private bool CheckTitleLine(out string? title, out IReadOnlyList<VarietyComponent> titleComponents, out int afterTitleIndex)
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
	public static readonly Reason UnknownEditingError = new("Unbekannter Fehler");
	public static readonly Reason NotVarietyLine = new("Inkompatibler Zeilentyp");
	public static readonly Reason CannotDeleteWithLinebreak = new("Inhalt kann nicht mit Zeilenumbruch gelöscht werden");
	public static readonly Reason CannotInsertMultipleLines = new("Kann keinen mehrzeiligen Inhalt einfügen");
	public static readonly Reason CannotPartiallyEditAttachments = new("Beim Bearbeiten von mehreren Akkorden müssen alle Akkorde vollständig ausgewählt sein");
	public static readonly Reason CannotInsertMultipleAttachments = new("Beim Einfügen darf maximal ein Akkord überschrieben werden");
	public static readonly Reason CannotInsertAttachmentsIntoRange = new("Beim Einfügen von Akkorden darf nichts selektiert sein");
	public static readonly Reason NoComponentFoundHere = new("Hier scheint keine Komponente zu sein");
	public static readonly Reason RemovingFromAttachmentFailed = new("Konnte den Akkord nicht kürzen");
	public static readonly Reason NoAttachmentAfter = new("Hiernach scheint kein Akkord zu sein");
	public static readonly Reason NoAttachmentBefore = new("Hiervor scheint kein Akkord zu sein");
	public static readonly Reason CouldNotMoveAttachment = new("Verschieben des Akkords fehlgeschlagen");
	public static readonly Reason CannotEditDifferentLineTypes = new("Mehrzeilige Bearbeitungen müssen auf dem gleichen Zeilentyp beginnen und enden");
	public static readonly Reason CannotMultiLineEditAttachments = new("Akkorde können nicht mehrzeilig bearbeitet werden");
	public static readonly Reason NoMoveNeeded = new("Kein Verschieben notwendig");
	public static readonly Reason AlreadyAttachmentAtTargetOffset = new("An der Zielposition ist bereits ein Akkord");

	private SpecialContentType GetAllowedTypes(Component? component)
	{
		//Hat eine der Komponenten Attachments?
		if (components.OfType<VarietyComponent>().Any(c => c.Attachments.Count > 0))
			return SpecialContentType.Text;

		return SpecialContentType.All;
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

		public DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter = null)
		{
			//Ist der Bereich leer?
			if (context.SelectionRange.Length == 0)
			{
				//Wird der Zeilenumbruch am Anfang entfernt?
				if (context.SelectionRange.Start == 0 && direction == DeleteDirection.Backward)
				{
					//Gibt es eine Zeile davor?
					var lineBefore = context.GetLineBefore?.Invoke();
					if (lineBefore is null)
						return DelayedMetalineEditResult.Fail(NoLineBefore);

					//Ist die vorherige Zeile leer?
					if (lineBefore is SheetEmptyLine)
					{
						//Lösche die vorherige Zeile
						return new DelayedMetalineEditResult(() =>
						{
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAtStart))
							{
								RemoveLineBefore = true,
							};
						});
					}

					//Ist die vorherige Zeile keine VarietyLine?
					if (lineBefore is not SheetVarietyLine varietyBefore)
						return DelayedMetalineEditResult.Fail(NotVarietyLine);

					//Bearbeitung wird funktionieren
					return new DelayedMetalineEditResult(() =>
					{
						//Füge alle Komponenten dieser Zeile an das Ende der vorherigen Zeile an
						var lastComponent = varietyBefore.components.Count == 0 ? null : varietyBefore.components[^1];
						varietyBefore.components.AddRange(Line.components);

						//Setze den Cursor an die Position, die mal das Ende der vorherigen Zeile war
						SpecialCursorPosition? specialCursorPosition = lastComponent is not null ? SpecialCursorPosition.Behind(lastComponent) : null;

						//Prüfe, ob die Komponenten zusammengefügt werden können
						if (lastComponent is not null && Line.components.Count > 0)
						{
							var mergeResult = lastComponent.TryMerge(Line.components[0], ContentOffset.FarEnd, formatter);
							if (mergeResult is not null)
							{
								//Entferne die zusammengefügte Komponente
								varietyBefore.components.Remove(Line.components[0]);

								//Setze den Cursor zwischen die neu zusammengefügten Komponenten
								specialCursorPosition = SpecialCursorPosition.FromStart(lastComponent, mergeResult.Value.LengthBefore, SpecialCursorVirtualPositionType.KeepRight);
							}
						}

						//Die vorherige Zeile muss neu gezeichnet werden
						varietyBefore.RaiseModifiedAndInvalidateCache();

						//Berechne Cursorposition
						int cursorPosition;
						if (specialCursorPosition is not null)
						{
							//Erzeuge Displayelemente, damit die Cursorposition berechnet werden kann
							varietyBefore.CreateDisplayLines(formatter);

							//Berechne Cursorposition
							cursorPosition = specialCursorPosition.Value.Calculate(varietyBefore, formatter);
						}
						else
						{
							//Setze den Cursor an die Position, die mal das Ende der vorherigen Zeile war
							cursorPosition = lastComponent?.DisplayRenderBounds.EndOffset ?? 0;
						}

						//Erzeuge das Ergebnis
						return new MetalineEditResult(new MetalineSelectionRange(varietyBefore.ContentEditor, SimpleRange.CursorAt(cursorPosition)))
						{
							RemoveLine = true,
						};
					});
				}

				//Wird der Zeilenumbruch am Ende entfernt?
				if (direction == DeleteDirection.Forward
					&& (Line.components.Count == 0 || context.SelectionRange.End >= Line.components[^1].DisplayRenderBounds.EndOffset))
				{
					//Gibt es eine Zeile danach?
					var lineAfter = context.GetLineAfter?.Invoke();
					if (lineAfter is null)
						return DelayedMetalineEditResult.Fail(NoLineAfter);

					//Ist die nächste Zeile leer?
					if (lineAfter is SheetEmptyLine)
					{
						//Lösche die nächste Zeile
						return new DelayedMetalineEditResult(() =>
						{
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAtStart))
							{
								RemoveLineAfter = true,
							};
						});
					}

					//Ist die nächste Zeile keine VarietyLine?
					if (lineAfter is not SheetVarietyLine varietyAfter)
						return DelayedMetalineEditResult.Fail(NotVarietyLine);

					//Bearbeitung wird funktionieren
					return new DelayedMetalineEditResult(() =>
					{
						//Füge alle Komponenten der nächsten Zeile an das Ende dieser Zeile an
						var lastComponent = Line.components.Count == 0 ? null : Line.components[^1];
						Line.components.AddRange(varietyAfter.components);

						//Setze den Cursor an die Position, die mal das Ende der vorherigen Zeile war
						SpecialCursorPosition? specialCursorPosition = lastComponent is not null ? SpecialCursorPosition.Behind(lastComponent) : null;

						//Prüfe, ob die Komponenten zusammengefügt werden können
						if (lastComponent is not null && varietyAfter.components.Count > 0)
						{
							var mergeResult = lastComponent.TryMerge(varietyAfter.components[0], ContentOffset.FarEnd, formatter);
							if (mergeResult is not null)
							{
								//Entferne die zusammengefügte Komponente
								Line.components.Remove(varietyAfter.components[0]);

								//Setze den Cursor zwischen die neu zusammengefügten Komponenten
								specialCursorPosition = SpecialCursorPosition.FromStart(lastComponent, mergeResult.Value.LengthBefore, SpecialCursorVirtualPositionType.KeepLeft);
							}
						}

						//Diese Zeile muss neu gezeichnet werden
						Line.RaiseModifiedAndInvalidateCache();

						//Berechne Cursorposition
						int cursorPosition;
						if (specialCursorPosition is not null)
						{
							//Erzeuge Displayelemente, damit die Cursorposition berechnet werden kann
							Line.CreateDisplayLines(formatter);

							//Berechne Cursorposition
							cursorPosition = specialCursorPosition.Value.Calculate(Line, formatter);
						}
						else
						{
							//Setze den Cursor an die Position, die mal das Ende der vorherigen Zeile war
							cursorPosition = lastComponent?.DisplayRenderBounds.EndOffset ?? 0;
						}

						//Setze den Cursor an die Position, die mal das Ende dieser Zeile war
						return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)))
						{
							RemoveLineAfter = true,
						};
					});
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

				//Wird ein ganzes Wort gelöscht?
				if (type == DeleteType.Word)
				{
					//Steht der Cursor am Anfang einer Komponente und soll diese gelöscht werden?
					if (startComponent is not null && direction == DeleteDirection.Forward)
					{
						//Lösche die Komponente
						context.SelectionRange = new SimpleRange(startComponent.DisplayRenderBounds.StartOffset, startComponent.DisplayRenderBounds.EndOffset);
					}

					//Steht der Cursor am Ende einer Komponente und soll diese gelöscht werden?
					else if (endComponent is not null && direction == DeleteDirection.Backward)
					{
						//Lösche die vorherige Komponente
						context.SelectionRange = new SimpleRange(endComponent.DisplayRenderBounds.StartOffset, endComponent.DisplayRenderBounds.EndOffset);
					}

					//Steht der Cursor mitten in einer Komponente?
					else if (previousComponent is not null)
					{
						//In welche Richtung soll gelöscht werden?
						if (direction == DeleteDirection.Forward)
						{
							//Kürze die Komponente nach dem Cursor
							context.SelectionRange = new SimpleRange(context.SelectionRange.Start, previousComponent.DisplayRenderBounds.EndOffset);
						}
						else
						{
							//Kürze die Komponente vor dem Cursor
							context.SelectionRange = new SimpleRange(previousComponent.DisplayRenderBounds.StartOffset, context.SelectionRange.Start);
						}
					}

					//Wurde keine passende Komponente gefunden?
					else
					{
						return DelayedMetalineEditResult.Fail(NoComponentFoundHere);
					}
				}
				else
				{
					//Lösche das Zeichen vor oder hinter dem Cursor
					if (direction == DeleteDirection.Forward)
					{
						//Steht der Cursor am Ende einer Komponente?
						if (endComponent is not null && nextComponent is not null)
						{
							//Lösche das erste Zeichen der ersten Komponente danach
							context.SelectionRange = new SimpleRange(endComponent.DisplayRenderBounds.EndOffset, nextComponent.DisplayRenderBounds.StartOffset + 1);
						}

						//Steht der Cursor mitten in einem langgestreckten Leerzeichen?
						else if (endComponent is null && previousComponent?.DisplayRenderBounds.EndOffset <= context.SelectionRange.Start && nextComponent is not null
							&& (previousComponent as VarietyComponent)?.Content.Type == ContentType.Space)
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
						else if (startComponent is null && previousComponent?.DisplayRenderBounds.EndOffset < context.SelectionRange.Start && prepreviousComponent is not null
							&& (previousComponent as VarietyComponent)?.Content.Type == ContentType.Space)
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
			}

			//Bearbeitung wird funktionieren
			return new DelayedMetalineEditResult(() => DeleteAndInsertContent(context, formatter, null, direction));
		}

		public DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			string content, ISheetEditorFormatter? formatter)
		{
			//Wird eine neue Zeile eingefügt?
			if (content == "\n")
			{
				//Dabei Inhalt überschreiben ist nicht erlaubt
				if (context.SelectionRange.Length > 0)
					return DelayedMetalineEditResult.Fail(CannotDeleteWithLinebreak);

				//Am Anfang?
				if (context.SelectionRange.Start == 0 && context.SelectionRange.End == 0)
				{
					//Erzeuge eine neue leere Zeile
					var newEmptyLine = new SheetEmptyLine();

					//Füge die neue Zeile davor ein
					return new DelayedMetalineEditResult(() =>
					{
						return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAtStart))
						{
							InsertLinesBefore = [newEmptyLine]
						};
					});
				}

				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
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
							var splitOffset = component.DisplayRenderBounds.GetContentOffset(context.SelectionRange.Start);
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
						return new MetalineEditResult(new MetalineSelectionRange(newEmptyLine, SimpleRange.CursorAtStart))
						{
							InsertLinesAfter = [newEmptyLine]
						};
					}

					//Entferne alle verschobenen Komponenten
					Line.components.RemoveRange(index, Line.components.Count - index);

					//Zeichne diese Zeile neu
					Line.RaiseModifiedAndInvalidateCache();

					//Füge die neue Zeile danach ein
					return new MetalineEditResult(new MetalineSelectionRange(newLine.ContentEditor, SimpleRange.CursorAtStart))
					{
						InsertLinesAfter = [newLine]
					};
				});
			}
			else if (content.Contains('\n'))
			{
				//Zeilenumbrüche müssen einzeln eingefügt werden
				return DelayedMetalineEditResult.Fail(CannotInsertMultipleLines);
			}

			//Bearbeitung wird funktionieren
			return new DelayedMetalineEditResult(() => DeleteAndInsertContent(context, formatter, content, default));
		}

		private MetalineEditResult DeleteAndInsertContent(SheetDisplayLineEditingContext context,
			ISheetEditorFormatter? formatter, string? content, DeleteDirection direction)
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
			if (content is not null && rightEdge is VarietyComponent varietyAfter
				&& varietyAfter.Content.Type == ContentType.Space && varietyAfter.Attachments.Count == 1)
			{
				//Erzeuge Komponenten für den Inhalt
				newContentComponents ??= CreateComponentsForContent(content, formatter);

				//Hat der Inhalt genau eine Textkomponente?
				if (newContentComponents.Count == 1 && newContentComponents[0].Content.Type != ContentType.Space)
				{
					//Übernimm das Attachment des Leerzeichens
					var attachment = varietyAfter.Attachments[0];
					varietyAfter.RemoveAttachment(attachment);
					newContentComponents[0].AddAttachment(attachment);

					//Füge die Komponenten vor dem rechten Rand ein
					Line.components.Insert(rightEdgeIndex, newContentComponents[0]);

					//Füge ggf. ein Leerzeichen vor den Inhalt ein
					if (leftEdge is VarietyComponent varietyBefore && varietyBefore.Content.Type != ContentType.Space)
						Line.components.Insert(rightEdgeIndex, new VarietyComponent(" "));

					//Anfügen erfolgreich
					content = null;
					addedContent = true;

					//Setze den Cursor an das Ende des eingefügten Inhalts
					//cursorPosition = context.SelectionRange.Start + newContentComponents[0].Content.GetLength(formatter);
					specialCursorPosition = SpecialCursorPosition.Behind(newContentComponents[0]);
				}
			}

			//Sonderfall: wird ein Text mit Attachment durch ein Leerzeichen ersetzt (oder umgekehrt)?
			if (content is not null && rangeStartsOnComponent && rangeEndsOnComponent && fullyInside.Count == 1
				&& fullyInside[0] is VarietyComponent varietyInside && varietyInside.Attachments.Count == 1
				&& (varietyInside.Content.Type == ContentType.Space) != string.IsNullOrWhiteSpace(content))
			{
				//Erzeuge Komponenten für den Inhalt
				newContentComponents ??= CreateComponentsForContent(content, formatter);
				
				//Hat der Inhalt genau eine Leerzeichenkomponente?
				if (newContentComponents.Count == 1 && newContentComponents[0].Content.Type != varietyInside.Content.Type)
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
					newContentComponents ??= CreateComponentsForContent(content, formatter);

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
						if (leftEdge.TryMerge(firstNewComponent, leftEdgeOverlapOffset.ContentOffset, formatter) is MergeResult mergeResult)
						{
							//Berechne die Gesamtlänge des eingefügten Inhalts
							var lastNewComponent = newContentComponents[^1];

							//Erste Komponente hinzugefügt
							newContentComponents.RemoveAt(0);

							//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
							specialCursorPosition = null;
							if (newContentComponents.Count > 0 && rightEdge?.TryMerge(lastNewComponent, ContentOffset.Zero, formatter) is not null)
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
									//Um wie viel hat sich der Inhalt verschoben?
									var contentAdjust = mergeResult.NewContent.GetLength(formatter) - mergeResult.LengthBefore;

									//Setze den Cursor an das Ende des eingefügten Inhalts im linken Rand
									var cursorOffset = leftEdgeOverlapOffset.ContentOffset + contentAdjust;
									if (leftEdge is VarietyComponent varietyEdge)
									{
										var length = varietyEdge.Content.GetLength(formatter);
										if (cursorOffset > length)
											cursorOffset = length;
									}
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

				//War das schon alles?
				else if (rightEdge == leftEdge)
				{
					//Setze den Cursor auf diese Position
					specialCursorPosition = SpecialCursorPosition.FromStart(leftEdge, leftEdgeOverlapOffset.ContentOffset,
						direction == DeleteDirection.Backward ? SpecialCursorVirtualPositionType.KeepRight : SpecialCursorVirtualPositionType.KeepLeft);
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
					newContentComponents ??= CreateComponentsForContent(content, formatter);

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
					newContentComponents ??= CreateComponentsForContent(content, formatter);

					//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
					if (rightEdge.TryMerge(newContentComponents[^1], ContentOffset.Zero, formatter) is not null)
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
						if (rightEdge is VarietyComponent varietyEdge)
						{
							var length = varietyEdge.Content.GetLength(formatter);
							if (cursorOffset > length)
								cursorOffset = length;
						}
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
				newContentComponents ??= CreateComponentsForContent(content, formatter);

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
				//if (rightEdge is not null && rightEdge != leftEdge)
				//{
				//	//Entferne die rechte Randkomponente
				//	Line.components.Remove(rightEdge);
				//	removedAnything = true;
				//}

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
						specialCursorPosition = SpecialCursorPosition.Before(rightEdge,
							direction == DeleteDirection.Backward ? SpecialCursorVirtualPositionType.KeepRight : SpecialCursorVirtualPositionType.KeepLeft);
					else if (leftEdge is not null)
						specialCursorPosition = SpecialCursorPosition.Behind(leftEdge,
							direction == DeleteDirection.Backward ? SpecialCursorVirtualPositionType.KeepRight : SpecialCursorVirtualPositionType.KeepLeft);
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
				if (component.TryMerge(nextComponent, ContentOffset.FarEnd, formatter) is not null)
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
				return MetalineEditResult.Fail(UnknownEditingError);

			//Zeile bearbeitet
			Line.RaiseModifiedAndInvalidateCache();
			Line.cachedLines = null;

			//Ist die Zeile jetzt leer?
			if (Line.components.Count == 0)
			{
				//Ersetze die Zeile durch eine Leerzeile
				var newLine = new SheetEmptyLine();
				return new MetalineEditResult(new(newLine, SimpleRange.CursorAt(0)))
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
			return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)));
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

		private List<VarietyComponent> CreateComponentsForContent(string content, ISheetEditorFormatter? formatter)
		{
			if (string.IsNullOrEmpty(content))
				throw new InvalidOperationException("Tried to insert empty content");

			//Trenne Wörter
			var result = content.SplitAlternating(char.IsWhiteSpace)
				.Select(w => VarietyComponent.FromString(w, formatter, Line.GetAllowedTypes(null)))
				.ToList();

			return result;
		}

		public ReasonBase? SupportsEdit(SheetDisplayMultiLineEditingContext context)
		{
			//Bin ich Start- oder Endzeile?
			if (context.StartLine == this)
			{
				//Die Endzeile muss vom gleichen Typ sein
				if (context.EndLine is ContentEditing)
					return null;
				else
					return CannotEditDifferentLineTypes;
			}
			else if (context.EndLine == this)
			{
				//Die Startzeile muss vom gleichen Typ sein
				if (context.StartLine is ContentEditing)
					return null;
				else
					return CannotEditDifferentLineTypes;
			}

			return null;
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

		public DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter = null)
		{
			//Ist der Bereich leer?
			if (context.SelectionRange.Length == 0)
			{
				//Wird ein Wort gelöscht?
				if (type == DeleteType.Word)
				{
					//Finde das nächste/vorherige Attachment
					var attachment = direction == DeleteDirection.Forward
						? FindAttachmentsInRange(SimpleRange.AllFromStart(context.SelectionRange.Start), formatter).FirstOrDefault()
						: FindAttachmentsInRange(SimpleRange.AllToEnd(context.SelectionRange.Start), formatter).LastOrDefault();

					//Attachment nicht gefunden?
					if (attachment.Component is null)
						return DelayedMetalineEditResult.Fail(direction == DeleteDirection.Forward ? NoAttachmentAfter : NoAttachmentBefore);

					//Bearbeitung wird funktionieren
					return new(() =>
					{
						//Lösche das Attachment
						attachment.Component.RemoveAttachment(attachment.Attachment);

						//Erzeuge die Displayelemente der Zeile neu
						Line.RaiseModifiedAndInvalidateCache();
						Line.CreateDisplayLines(formatter);

						//Setze den Cursor an die Stelle des gelöschten Attachments
						if (direction == DeleteDirection.Forward)
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(attachment.Attachment.RenderBounds.StartOffset)));
						else
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(attachment.Attachment.RenderBounds.AfterOffset)));
					});
				}

				//Erweitere einfach die Auswahl um ein Zeichen nach rechts oder links
				if (direction == DeleteDirection.Forward)
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
				return TryEditAttachment(attachments[0].Component, attachments[0].Attachment, context.SelectionRange, null, formatter);
			}

			//Liegen mehrere Attachments im Bereich?
			if (attachments.Count > 1)
			{
				//Liegen sie nicht alle komplett im Bereich?
				if (!attachments.All(a => a.Attachment.RenderBounds.StartOffset >= context.SelectionRange.Start && a.Attachment.RenderBounds.AfterOffset <= context.SelectionRange.End))
					return DelayedMetalineEditResult.Fail(CannotPartiallyEditAttachments);

				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
					//Lösche die Attachments
					foreach (var (component, attachment) in attachments)
						component.RemoveAttachment(attachment);

					//Modified-Event
					Line.RaiseModifiedAndInvalidateCache();
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(context.SelectionRange.Start)));
				});
			}

			//Finde das nächste Attachment und verschiebe es nach links
			return TryFindAndMoveNextAttachment(context.SelectionRange, 0, formatter);
		}

		public DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			string content, ISheetEditorFormatter? formatter)
		{
			//Finde alle Attachments im Bereich
			var attachments = FindAttachmentsInRange(context.SelectionRange, formatter).ToList();

			//Liegt genau ein Attachment im Bereich?
			if (attachments.Count == 1)
			{
				//Bearbeite das Attachment
				return TryEditAttachment(attachments[0].Component, attachments[0].Attachment, context.SelectionRange, content, formatter);
			}

			//Liegt mehr als ein Attachment im Bereich?
			if (attachments.Count > 1)
			{
				//Bearbeiten von mehreren Attachments nicht möglich
				return DelayedMetalineEditResult.Fail(CannotInsertMultipleAttachments);
			}

			//Werden Whitespaces eingefügt?
			if (string.IsNullOrWhiteSpace(content))
			{
				//Finde das nächste Attachment und verschiebe es
				return TryFindAndMoveNextAttachment(context.SelectionRange, content.Length, formatter);
			}

			//Ist der Bereich eine Selektion?
			if (context.SelectionRange.Length > 0)
				return DelayedMetalineEditResult.Fail(CannotInsertAttachmentsIntoRange);

			//Finde die Komponente, in der der Inhalt eingefügt werden soll
			var component = Line.components.OfType<VarietyComponent>()
				.FirstOrDefault(c => c.DisplayRenderBounds.StartOffset <= context.SelectionRange.Start && c.DisplayRenderBounds.EndOffset > context.SelectionRange.End);
			if (component is null)
				return DelayedMetalineEditResult.Fail(NoComponentFoundHere);

			//Liegt ein Attachment direkt vor oder hinter der Position?
			var before = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().LastOrDefault(a => a.RenderBounds.AfterOffset == context.SelectionRange.Start - 1);
			var after = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().FirstOrDefault(a => a.RenderBounds.StartOffset == context.SelectionRange.End);
			if (before is not null)
			{
				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
					//Füge den Inhalt ans Ende des Attachments an
					before.InsertContent(content, ContentOffset.FarEnd, formatter);

					//Erzeuge die Displayelemente der Zeile neu
					Line.RaiseModifiedAndInvalidateCache();
					Line.CreateDisplayLines(formatter);

					//Setze den Cursor hinter das Attachment
					var cursorPosition = before.RenderBounds.AfterOffset;
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)));
				});
			}
			else if (after is not null)
			{
				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
					//Füge den Inhalt an den Anfang des Attachments an
					after.InsertContent(content, ContentOffset.Zero, formatter);

					//Erzeuge die Displayelemente der Zeile neu
					Line.RaiseModifiedAndInvalidateCache();
					Line.CreateDisplayLines(formatter);

					//Setze den Cursor hinter den eingefügten Text
					var cursorPosition = after.RenderBounds.StartOffset + content.Length;
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)));
				});
			}
			else
			{
				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
					//Erzeuge ein neues Attachment
					var componentOffset = component.DisplayRenderBounds.GetContentOffset(context.SelectionRange.Start);
					var newAttachment = VarietyComponent.VarietyAttachment.FromString(componentOffset.ContentOffset, content, formatter);
					component.AddAttachment(newAttachment);

					//Erzeuge die Displayelemente der Zeile neu
					Line.RaiseModifiedAndInvalidateCache();
					Line.CreateDisplayLines(formatter);

					//Setze den Cursor hinter das eingefügte Attachment
					var cursorPosition = newAttachment.RenderBounds.AfterOffset;
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)));
				});
			}
		}

		private IEnumerable<(VarietyComponent Component, VarietyComponent.VarietyAttachment Attachment)> FindAttachmentsInRange(SimpleRange range, ISheetEditorFormatter? formatter)
		{
			//Finde alle Attachments im Bereich
			foreach (var component in Line.components)
			{
				//Liegt die Komponente vor dem Bereich?
				if (component.TotalRenderBounds.AfterOffset < range.Start)
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
					if (attachment.RenderBounds.AfterOffset < range.Start)
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

		private DelayedMetalineEditResult TryEditAttachment(VarietyComponent component, VarietyComponent.VarietyAttachment attachment,
			SimpleRange selectionRange, string? content, ISheetEditorFormatter? formatter)
		{
			//Werden Whitespaces am Anfang des Attachments eingefügt oder gelöscht?
			if (string.IsNullOrWhiteSpace(content) && selectionRange.End <= attachment.RenderBounds.StartOffset)
			{
				//Verschiebe das Attachment
				return TryMoveAttachment(component, attachment, selectionRange, selectionRange, content?.Length ?? 0, formatter);
			}

			//Werden Whitespaces am Ende des Attachments eingefügt oder gelöscht?
			if (string.IsNullOrWhiteSpace(content) && selectionRange.Start >= attachment.RenderBounds.AfterOffset)
			{
				//Verschiebe das nächste Attachment
				return TryFindAndMoveNextAttachment(selectionRange, content?.Length ?? 0, formatter);
			}

			//Kürze das Attachment
			var attachmentOffset = attachment.RenderBounds.GetContentOffset(selectionRange.Start);
			var selectionContentLength = attachment.RenderBounds.GetContentOffset(selectionRange.End) - attachmentOffset;
			var attachmentDisplayLength = attachment.RenderBounds.Length;
			var removedLength = 0;
			Action? doRemove = null;
			if (selectionRange.Length > 0)
			{
				doRemove = attachment.TryRemoveContent(attachmentOffset, selectionContentLength, formatter);
				if (doRemove is null)
					return DelayedMetalineEditResult.Fail(RemovingFromAttachmentFailed);
				else if (content is null)
					removedLength = selectionRange.Length;
			}

			//Bearbeitung wird funktionieren
			return new DelayedMetalineEditResult(() =>
			{
				//Lösche ggf. den Inhalt
				doRemove?.Invoke();

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
				var cursorPosition = selectionRange.Start - attachmentDisplayLength + removedLength;
				if (!attachment.IsEmpty)
					cursorPosition += attachment.RenderBounds.Length;

				//Fertig
				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)));
			});
		}

		private DelayedMetalineEditResult TryFindAndMoveNextAttachment(SimpleRange selection, int contentMove, ISheetEditorFormatter? formatter)
		{
			//Finde das nächste Attachment
			var nextAttachment = FindAttachmentsInRange(new SimpleRange(selection.Start + 1, int.MaxValue), formatter).FirstOrDefault();
			if (nextAttachment.Attachment is null)
				return DelayedMetalineEditResult.Fail(NoAttachmentAfter);

			//Verschiebe das Attachment
			return TryMoveAttachment(nextAttachment.Component, nextAttachment.Attachment,
				new SimpleRange(nextAttachment.Attachment.RenderBounds.StartOffset - selection.Length, nextAttachment.Attachment.RenderBounds.StartOffset),
				selection, contentMove, formatter);
		}

		private DelayedMetalineEditResult TryMoveAttachment(VarietyComponent component, VarietyComponent.VarietyAttachment attachment,
			SimpleRange editSelection, SimpleRange cursorSelection, int contentMove, ISheetEditorFormatter? formatter)
		{
			//Keine Verschiebung?
			if (contentMove - editSelection.Length == 0)
				return DelayedMetalineEditResult.Fail(NoMoveNeeded);

			//Finde die Zielkomponente
			var targetGenericComponent = contentMove > 0
				? Line.components.Select((c, i) => (Component: c, Index: i)).FirstOrDefault(c => c.Component.TotalRenderBounds.AfterOffset > editSelection.Start + contentMove)
				: Line.components.Select((c, i) => (Component: c, Index: i)).LastOrDefault(c => c.Component.TotalRenderBounds.StartOffset <= editSelection.Start);
			if (targetGenericComponent.Component is not VarietyComponent targetComponent)
				return DelayedMetalineEditResult.Fail(NoComponentFoundHere);

			//Finde den Zieloffset
			var virtualTargetOffset = targetComponent.DisplayRenderBounds.GetContentOffset(editSelection.Start + contentMove);
			var targetOffset = virtualTargetOffset.ContentOffset;
			if (virtualTargetOffset.VirtualOffset != 0)
			{
				//Verschiebung nach links oder rechts?
				if (contentMove - editSelection.Length > 0)
				{
					//Verschiebe das Attachment eins weiter nach rechts
					targetOffset += ContentOffset.One;
				}
			}

			//Ist das Attachment schon am Ende der Komponente?
			if (targetOffset >= targetComponent.Content.GetLength(formatter))
			{
				//Finde die nächste Komponente
				var nextComponentIndex = targetGenericComponent.Index + 1;
				if (nextComponentIndex >= Line.components.Count)
					return DelayedMetalineEditResult.Fail(NoComponentFoundHere);
				var nextComponent = Line.components[nextComponentIndex] as VarietyComponent;
				if (nextComponent is null)
					return DelayedMetalineEditResult.Fail(NoComponentFoundHere);

				//Verschiebe an den Anfang der nächsten Komponente
				targetComponent = nextComponent;
				targetOffset = ContentOffset.Zero;
			}

			//Ist am Zieloffset kein Platz?
			if (targetComponent.Attachments.Any(a => a.Offset == targetOffset))
			{
				//Verschiebung nicht möglich
				return DelayedMetalineEditResult.Fail(AlreadyAttachmentAtTargetOffset);
			}

			//Bewege das Attachment
			var moveAction = TryMoveAttachmentTo(component, attachment, targetComponent, targetOffset, formatter);
			if (moveAction is null)
				return DelayedMetalineEditResult.Fail(CouldNotMoveAttachment);
			return new DelayedMetalineEditResult(() =>
			{
				//Bewege das Attachment
				moveAction();

				//Zeile bearbeitet
				Line.RaiseModifiedAndInvalidateCache();

				//Ergebnis
				var cursorPosition = cursorSelection.Start + contentMove;
				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(cursorPosition)));
			});
		}

		private Action? TryMoveAttachmentTo(VarietyComponent component, VarietyComponent.VarietyAttachment attachment,
			VarietyComponent targetComponent, ContentOffset targetOffset, ISheetEditorFormatter? formatter)
		{
			//Wird das Attachment nicht verschoben?
			if (targetComponent == component && targetOffset == attachment.Offset)
				return null;

			//Hat sich die Komponente nicht verändert?
			if (component == targetComponent)
			{
				return () =>
				{
					//Verschiebe das Attachment
					attachment.SetOffset(targetOffset);

					//Modified-Event
					Line.RaiseModifiedAndInvalidateCache();
				};
			}

			return () =>
			{
				//Verschiebe das Attachment in die andere Komponente
				component.RemoveAttachment(attachment);
				attachment.SetOffset(targetOffset);
				targetComponent.AddAttachment(attachment);

				//Modified-Event
				Line.RaiseModifiedAndInvalidateCache();
			};
		}

		public ReasonBase? SupportsEdit(SheetDisplayMultiLineEditingContext context)
		{
			//Bin ich Start- oder Endzeile?
			if (context.StartLine == this)
			{
				//Die Endzeile muss vom gleichen Typ sein
				if (context.EndLine is AttachmentEditing)
					return CannotMultiLineEditAttachments;
				else
					return CannotEditDifferentLineTypes;
			}
			else if (context.EndLine == this)
			{
				//Die Startzeile muss vom gleichen Typ sein
				if (context.StartLine is AttachmentEditing)
					return CannotMultiLineEditAttachments;
				else
					return CannotEditDifferentLineTypes;
			}

			return null;
		}
	}
	#endregion

	#region Content
	public enum ContentType
	{
		Word,
		Space,
		Punctuation,
		Chord,
		Fingering,
	}

	[Flags]
	public enum SpecialContentType
	{
		None = 0,
		Text = 1,
		Chord = 2,
		Fingering = 4,

		All = Text | Chord | Fingering,
	}

	internal readonly record struct MergeResult(ComponentContent NewContent, ContentOffset LengthBefore)
	{
		public ContentOffset MergeLengthBefore { get; init; }
	}

	internal readonly struct ComponentContent
	{
		public const string PUNCTUATION = ",.-><!?\"";

		public string? Text { get; }
		public Chord? Chord { get; }
		public Fingering? Fingering { get; }

		public bool IsEmpty => Chord is null && Fingering is null && string.IsNullOrEmpty(Text);

		public ContentType Type => Chord is not null ? ContentType.Chord
			: Fingering is not null ? ContentType.Fingering
			: string.IsNullOrWhiteSpace(Text) ? ContentType.Space
			: Text.All(PUNCTUATION.Contains) ? ContentType.Punctuation
			: ContentType.Word;

		public ComponentContent(string text)
		{
			Text = text;
		}

		public ComponentContent(Chord chord)
		{
			Chord = chord;
		}

		public ComponentContent(Fingering fingering)
		{
			Fingering = fingering;
		}

		public static ComponentContent FromString(string content, ISheetEditorFormatter? formatter, SpecialContentType allowedTypes = SpecialContentType.All)
		{
			if ((allowedTypes & SpecialContentType.Chord) != 0)
			{
				//Versuche den Inhalt als Akkord zu lesen
				var chordLength = Chord.TryRead(formatter, content, out var chord);
				if (chord is not null && chordLength == content.Length)
				{
					//Der Inhalt ist ein Akkord
					return new(chord);
				}
			}

			if ((allowedTypes & SpecialContentType.Fingering) != 0)
			{
				//Versuche den Inhalt als Fingering zu lesen
				var fingeringLength = Fingering.TryRead(formatter, content, out var fingering);
				if (fingering is not null && fingeringLength == content.Length)
				{
					//Der Inhalt ist ein Fingering
					return new(fingering);
				}
			}

			//Der Inhalt ist kein Akkord
			return new(content);
		}

		public static int TryRead(ReadOnlySpan<char> content, out ComponentContent? result, ISheetEditorFormatter? formatter)
		{
			if (content.Length == 0)
			{
				result = null;
				return -1;
			}

			//Prüfe auf Akkord
			var read = Chord.TryRead(formatter, content, out var chord);
			if (read > 0 && chord is not null)
			{
				result = new ComponentContent(chord);
				return read;
			}

			//Prüfe auf Fingering
			read = Fingering.TryRead(formatter, content, out var fingering);
			if (read > 0 && fingering is not null)
			{
				result = new ComponentContent(fingering);
				return read;
			}

			//Prüfe auf Leerzeichen
			if (char.IsWhiteSpace(content[0]))
			{
				for (read = 1; read < content.Length && char.IsWhiteSpace(content[read]); read++) ;

				result = new ComponentContent(new string(content[0..read]));
				return read;
			}

			//Prüfe auf Interpunktion
			if (PUNCTUATION.Contains(content[0]))
			{
				for (read = 1; read < content.Length && PUNCTUATION.Contains(content[read]); read++) ;

				result = new ComponentContent(new string(content[0..read]));
				return read;
			}

			//Lese Wort
			for (read = 1; read < content.Length && !char.IsWhiteSpace(content[read]) && !PUNCTUATION.Contains(content[read]); read++) ;
			result = new ComponentContent(new string(content[0..read]));
			return read;
		}

		public static ComponentContent CreateSpace(ContentOffset length, ISheetEditorFormatter? formatter)
			=> new ComponentContent(new string(' ', length.Value));

		public SheetDisplayLineElement CreateElement(SheetDisplaySliceInfo sliceInfo, ISheetFormatter? formatter)
		{
			if (Chord is not null)
				return new SheetDisplayLineChord(Chord)
				{
					Slice = sliceInfo
				};
			else if (Fingering is not null)
				return new SheetDisplayLineFingering(Fingering)
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

		public SheetDisplayLineElement CreateDisplayElementPart(SheetDisplaySliceInfo sliceId, ContentOffset offset, ContentOffset length, ISheetBuilderFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = ToString(formatter);
			if (offset == ContentOffset.Zero && length.Value >= textContent.Length)
				return CreateElement(sliceId, formatter);

			//Bilde Substring
			var subContent = new ComponentContent(
				textContent.Substring(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value)));
			return subContent.CreateElement(sliceId, formatter);
		}

		public ContentOffset GetLength(ISheetFormatter? formatter)
			=> new(ToString(formatter).Length);

		internal ComponentContent? RemoveContent(ContentOffset offset, ContentOffset length, SpecialContentType allowedTypes, ISheetEditorFormatter? formatter)
		{
			if (offset < ContentOffset.Zero)
			{
				length -= offset;
				offset = ContentOffset.Zero;
			}

			if (length <= ContentOffset.Zero)
				return null;

			//Textinhalt
			var textContent = ToString(formatter);
			if (textContent is null) return null;

			//Kürze den Textinhalt
			if (offset.Value >= textContent.Length) return null;
			var newContent = textContent.Remove(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value));
			if (newContent == textContent) return null;

			//Setze den neuen Inhalt
			return FromString(newContent, formatter, allowedTypes);
		}

		internal MergeResult MergeContents(string content, ContentOffset offset, SpecialContentType allowedTypes, ISheetEditorFormatter? formatter)
		{
			//Textinhalt
			var textContent = ToString(formatter);

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset.Value, textContent.Length);
			var newContent = textContent[0..stringOffset] + content;
			if (offset.Value < textContent.Length)
				newContent += textContent[stringOffset..];

			//Ergebnis
			return new(FromString(newContent, formatter, allowedTypes), new ContentOffset(textContent.Length))
			{
				MergeLengthBefore = new ContentOffset(content.Length)
			};
		}

		internal MergeResult MergeContents(ComponentContent content, ContentOffset offset, SpecialContentType allowedTypes, ISheetEditorFormatter? formatter)
		{
			//Textinhalt
			var textContent = ToString(formatter);
			var lengthBefore = textContent.Length;
			var afterTextContent = content.ToString(formatter);
			var stringOffset = Math.Min(offset.Value, textContent.Length);

			//Sonderfall: Wird ein Text hinten an einen Akkord angefügt?
			if (Chord is not null && content.Type is ContentType.Word && stringOffset >= textContent.Length)
			{
				//Verwende den ursprünglichen Text des Akkords
				textContent = Chord.OriginalText;
				stringOffset = textContent.Length;
			}

			//Füge den Textinhalt hinzu
			var newContent = textContent[0..stringOffset] + afterTextContent;
			if (offset.Value < textContent.Length)
				newContent += textContent[stringOffset..];

			//Ergebnis
			return new(FromString(newContent, formatter, allowedTypes), new ContentOffset(lengthBefore))
			{
				MergeLengthBefore = new ContentOffset(afterTextContent?.Length ?? 0)
			};
		}

		internal (ComponentContent NewContent, ComponentContent EndContent)? SplitEnd(ContentOffset offset, SpecialContentType allowedTypes, SpecialContentType endAllowedTypes, ISheetEditorFormatter? formatter)
		{
			//Textinhalt
			var textContent = ToString(formatter);

			if (offset.Value >= textContent.Length)
				return null;

			//Trenne den Textinhalt auf
			var newContent = textContent[..offset.Value];
			var newEndContent = textContent[offset.Value..];

			//Erzeuge das neue Ende
			return (
				FromString(newContent, formatter, allowedTypes),
				FromString(newEndContent, formatter, endAllowedTypes));
		}

		#region Operators
		public override string ToString() => ToString(null);
		public string ToString(ISheetFormatter? formatter)
			=> Text ?? Chord?.ToString(formatter) ?? Fingering?.ToString(formatter) ?? string.Empty;
		#endregion
	}
	#endregion

	#region ComponentCollection
	private class ComponentCollection : IList<Component>
	{
		private readonly SheetVarietyLine owner;
		private readonly List<Component> components;

		public int Count => components.Count;
		public bool IsReadOnly => false;

		public Component this[int index]
		{
			get => components[index];
			set => components[index] = value;
		}

		public ComponentCollection(SheetVarietyLine owner)
		{
			this.owner = owner;
			this.components = new();
		}

		public ComponentCollection(SheetVarietyLine owner, IEnumerable<Component> components)
		{
			this.owner = owner;
			this.components = new(components.Select(c =>
			{
				c.Owner = owner;
				return c;
			}));
		}

		public bool Contains(Component item) => components.Contains(item);
		public int IndexOf(Component item) => components.IndexOf(item);
		public void CopyTo(Component[] array, int arrayIndex) => components.CopyTo(array, arrayIndex);

		public void Add(Component item)
		{
			components.Add(item);
			item.Owner = owner;
		}

		public void AddRange(IEnumerable<Component> components)
			=> this.components.AddRange(components.Select(c =>
		{
			c.Owner = owner;
			return c;
		}));

		public void InsertRange(int index, IEnumerable<Component> components)
			=> this.components.InsertRange(index, components.Select(c =>
		{
			c.Owner = owner;
			return c;
		}));

		public void Insert(int index, Component item)
		{
			components.Insert(index, item);
			item.Owner = owner;
		}

		public bool Remove(Component item)
		{
			if (!components.Remove(item))
				return false;

			if (item.Owner == owner)
				item.Owner = null;

			return true;
		}

		public void RemoveAt(int index)
		{
			var item = components[index];
			components.RemoveAt(index);

			if (item.Owner == owner)
				item.Owner = null;
		}

		public void RemoveRange(int index, int count)
		{
			var end = index + count;
			for (var i = index; i < end; i++)
			{
				var component = components[i];
				if (component.Owner == owner)
					component.Owner = null;
			}

			components.RemoveRange(index, count);
		}

		public void RemoveAll(Func<Component, bool> match)
			=> components.RemoveAll(c =>
		{
			if (!match(c))
				return false;

			if (c.Owner == owner)
				c.Owner = null;

			return true;
		});

		public void Clear()
		{
			foreach (var component in components)
				if (component.Owner == owner)
					component.Owner = null;

			components.Clear();
		}

		public IEnumerator<Component> GetEnumerator() => components.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
	#endregion

	#region Components
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

	public abstract class Component : SheetLineComponent
	{
		internal SheetVarietyLine? Owner { get; set; }

		public abstract bool IsEmpty { get; }

		internal abstract DisplayRenderBounds DisplayRenderBounds { get; }
		internal abstract RenderBounds TotalRenderBounds { get; }
		internal abstract IReadOnlyList<SheetDisplayLineElement> ContentElements { get; }

		internal abstract void BuildLines(LineBuilders builders, int componentIndex, ISheetBuilderFormatter? formatter);

		internal abstract bool TryRemoveContent(ContentOffset offet, ContentOffset length, ISheetEditorFormatter? formatter);
		internal abstract bool TryReplaceContent(ComponentContent newContent, ISheetEditorFormatter? formatter);
		internal abstract MergeResult? TryMerge(Component next, ContentOffset offset, ISheetEditorFormatter? formatter);
		internal abstract Component SplitEnd(ContentOffset offset, ISheetEditorFormatter? formatter);

		internal class LineBuilders
		{
			private int breakPointIndex;

			public SheetVarietyLine Owner { get; }
			public SheetDisplayTextLine.Builder TextLine { get; }
			public SheetDisplayChordLine.Builder ChordLine { get; }

			public IEnumerable<SheetDisplayLineBuilderBase> AllLines => [TextLine, ChordLine];

			public bool IsTitleLine { get; init; }
			public bool IsRenderingTitle { get; set; }

			public int CurrentLength => Math.Max(TextLine.CurrentLength, ChordLine.CurrentLength);

			public LineBuilders(SheetVarietyLine owner, SheetDisplayTextLine.Builder textLine, SheetDisplayChordLine.Builder chordLine)
			{
				Owner = owner;
				TextLine = textLine;
				ChordLine = chordLine;
			}

			public void AddBreakPoint(SheetLineComponent component, int textLineOffset, int chordLineOffset, ISheetFormatter? formatter = null)
			{
				var index = breakPointIndex++;

				TextLine.Append(new SheetDisplayLineBreakPoint(index, textLineOffset)
				{
					Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
				});
				ChordLine.Append(new SheetDisplayLineBreakPoint(index, chordLineOffset)
				{
					Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
				});
			}

			public (SheetDisplayLineBreakPoint Text, SheetDisplayLineBreakPoint Attachment) CreateBreakPoints(SheetLineComponent component, int textLineOffset, int chordLineOffset)
			{
				var index = breakPointIndex++;

				return (
					new SheetDisplayLineBreakPoint(index, textLineOffset)
					{
						Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
					},
					new SheetDisplayLineBreakPoint(index, chordLineOffset)
					{
						Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
					});
			}
		}
	}

	public sealed class VarietyComponent : Component
	{
		private readonly List<Attachment> attachments = new();
		public IReadOnlyList<Attachment> Attachments => attachments;

		internal ComponentContent Content { get; private set; }

		public override bool IsEmpty => Content.IsEmpty;

		private DisplayRenderBounds displayRenderBounds = DisplayRenderBounds.Empty;
		internal override DisplayRenderBounds DisplayRenderBounds => displayRenderBounds;

		private RenderBounds totalRenderBounds = RenderBounds.Empty;
		internal override RenderBounds TotalRenderBounds => totalRenderBounds;

		private List<SheetDisplayLineElement> contentElements = new();
		internal override IReadOnlyList<SheetDisplayLineElement> ContentElements => contentElements;

		internal VarietyComponent(ComponentContent content)
		{
			Content = content;
		}

		public VarietyComponent(string text)
		{
			Content = new(text);
		}

		public VarietyComponent(Chord chord)
		{
			Content = new(chord);
		}

		public static VarietyComponent FromString(string content, ISheetEditorFormatter? formatter, SpecialContentType allowedTypes = SpecialContentType.All)
			=> new VarietyComponent(ComponentContent.FromString(content, formatter, allowedTypes));

		public static VarietyComponent CreateSpace(ContentOffset length, ISheetEditorFormatter? formatter)
			=> new VarietyComponent(ComponentContent.CreateSpace(length, formatter));

		#region Display
		internal override void BuildLines(LineBuilders builders, int componentIndex, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Verlängere die Akkordzeile auf die Länge der Textzeile (macht das Spacing einfacher und schließt keinen Fall aus)
			builders.ChordLine.ExtendLength(builders.TextLine.CurrentLength, 0);

			//Finde das erste Attachment
			(Attachment Attachment, SheetDisplayLineElement Display) firstAttachment = attachments
				.Select((a, i) => (Attachment: a, Display: a.CreateDisplayAttachment(out _, formatter)))
				.FirstOrDefault(a => a.Display is not null)!;
			(SheetDisplayLineBreakPoint Text, SheetDisplayLineBreakPoint Attachment)? firstAttachmentBreakpoint = null;
			int firstAttachmentOffset = 0;
			if (firstAttachment.Attachment is null)
			{
				//Die Komponente hat keine Attachments

				//Ist die Komponente nur ein einzelnes Satzzeichen und war die vorherige Komponente ein Wort?
				if (Content.Type == ContentType.Punctuation && Content.Text is not null
					&& builders.TextLine.CurrentLength == builders.TextLine.CurrentNonSpaceLength)
				{
					//Schreibe nur das Satzzeichen
					var textStartLength = builders.TextLine.CurrentLength;
					var displayText = new SheetDisplayLineText(Content.Text)
					{
						Slice = new SheetDisplaySliceInfo(this, ContentOffset.Zero)
					};
					builders.TextLine.Append(displayText);

					//Berechne Render Bounds
					contentElements = [displayText];
					displayRenderBounds = new(textStartLength, builders.TextLine.CurrentLength, contentElements);
					totalRenderBounds = new(displayRenderBounds.StartOffset, displayRenderBounds.EndOffset);
					return;
				}

				//Füge direkt einen Breakpoint ein
				builders.AddBreakPoint(this, 0, 0, formatter);
			}
			else
			{
				//An welchen Offset soll das Attachment geschrieben werden?
				var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, firstAttachment.Display)
					?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
				firstAttachmentOffset = builders.ChordLine.CurrentNonSpaceLength + spaceBefore;

				//Wie viel Platz wird auf der Akkordzeile benötigt, damit das Attachment passt?
				var required = firstAttachmentOffset - firstAttachment.Attachment.Offset.Value;
				builders.ChordLine.ExtendLength(required, 0, formatter);

				//Verlängere die Textzeile auch um diese Differenz
				builders.TextLine.ExtendLength(required, 0, formatter);

				//Füge einen Breakpoint auf der Textzeile ein
				firstAttachmentBreakpoint = builders.CreateBreakPoints(this, 0, firstAttachment.Attachment.Offset.Value);
				builders.TextLine.Append(firstAttachmentBreakpoint.Value.Text, formatter);
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
					builders.ChordLine.EnsureSpaceBefore(spaceBefore, formatter);

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

					//Füge beim ersten Attachment den Breakpoint ein
					if (firstAttachmentBreakpoint is not null)
					{
						builders.ChordLine.Append(firstAttachmentBreakpoint.Value.Attachment with
						{
							StartingPointOffset = builders.ChordLine.CurrentLength - firstAttachmentBreakpoint.Value.Text.DisplayOffset,
						}, formatter);
						firstAttachmentBreakpoint = null;
					}

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
				var slice = new SheetDisplaySliceInfo(this, currentOffset, false);
				var displayAttachment = currentAttachment.CreateDisplayAttachment(out var setAttachmentRenderBounds, formatter);

				//Erzeuge den Inhalt
				var displayContent = Content.CreateDisplayElementPart(slice, currentAttachment.Offset, textLength, formatter);
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
			var contentOffset = element.Slice?.ContentOffset ?? ContentOffset.Zero;
			if (!isCurrentlyWritingTitle)
			{
				if (!text.StartsWith('['))
					yield break; //nur Text
				
				//Trenne öffnende Klammer
				yield return new SheetDisplayLineSegmentTitleBracket("[", true)
				{
					Slice = textElement.Slice,
				};
				if (text.Length == 1)
					yield break;

				sliceIndex++;
				contentOffset += ContentOffset.One;
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
						ContentOffset = contentOffset,
					},
				};
				contentOffset += new ContentOffset(titleText.Length);

				//Trenne schließende Klammer
				yield return new SheetDisplayLineSegmentTitleBracket("]", false)
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = contentOffset,
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
						ContentOffset = contentOffset,
					},
				};
				contentOffset += new ContentOffset(titleText.Length);
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

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return null;
			}

			private static void SetRenderBounds(RenderBounds _) { }
		}
		#endregion

		#region Editing
		internal override bool TryReplaceContent(ComponentContent newContent, ISheetEditorFormatter? formatter)
		{
			//Passt der Inhaltstyp nicht?
			if (newContent.Chord is not null && Owner?.GetAllowedTypes(this).HasFlag(SpecialContentType.Chord) != true)
				return false;
			if (newContent.Fingering is not null && Owner?.GetAllowedTypes(this).HasFlag(SpecialContentType.Fingering) != true)
				return false;
			if (newContent.Type != Content.Type)
				return false;

			//Ersetze den Inhalt
			Content = newContent;

			//Entferne alle Attachments, die außerhalb des Inhalts liegen
			var lengthAfter = Content.GetLength(formatter);
			attachments.RemoveAll(a => a.Offset >= lengthAfter);
			return true;
		}

		internal override bool TryRemoveContent(ContentOffset offset, ContentOffset length, ISheetEditorFormatter? formatter)
		{
			//Speichere die Länge vor der Bearbeitung
			var lengthBefore = Content.GetLength(formatter);
			if (length == ContentOffset.Zero || offset >= lengthBefore) return false;

			//Entferne den Inhalt
			var removedContent = Content.RemoveContent(offset, length, Owner?.GetAllowedTypes(this) ?? SpecialContentType.None, formatter);
			if (removedContent is null)
				return false;
			Content = removedContent.Value;

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

		internal override MergeResult? TryMerge(Component next, ContentOffset offset, ISheetEditorFormatter? formatter)
		{
			//Ist das nachfolgende Element kein VarietyComponent?
			if (next is not VarietyComponent varietyMerge)
				return null;

			//Prüfe, zu welchen Typen der Inhalt zusammengeführt werden kann
			var mergeType = CanMergeTo(Content, varietyMerge.Content)
				& (Owner?.GetAllowedTypes(this) ?? SpecialContentType.Text);

			//Kann der Inhalt gar nicht zusammengeführt werden?
			if (mergeType == SpecialContentType.None)
				return null;

			//Füge Inhalt zusammen
			var lengthBefore = Content.GetLength(formatter);
			var mergeLengthBefore = varietyMerge.Content.GetLength(formatter);
			var mergeResult = Content.MergeContents(varietyMerge.Content, offset, mergeType, formatter);

			////Passen die Inhaltstypen nicht zusammen?
			//if (!CanAppendTo(Content, varietyMerge.Content, mergeResult.NewContent))
			//	return null;

			Content = mergeResult.NewContent;
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
			return mergeResult;
		}

		internal override Component SplitEnd(ContentOffset offset, ISheetEditorFormatter? formatter)
		{
			//Trenne den Inhalt
			var allowedTypes = Owner?.GetAllowedTypes(this) ?? SpecialContentType.None;
			var newContents = Content.SplitEnd(offset, allowedTypes, allowedTypes, formatter);
			if (newContents is null)
				throw new ArgumentOutOfRangeException();

			Content = newContents.Value.NewContent;
			var newEnd = new VarietyComponent(newContents.Value.EndContent);

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

		private static SpecialContentType CanMergeTo(ComponentContent left, ComponentContent right)
		{
			//Leerzeichen können nur mit Leerzeichen kombiniert werden
			if (left.Type == ContentType.Space)
				return right.Type == ContentType.Space ? SpecialContentType.Text : SpecialContentType.None;
			else if (right.Type == ContentType.Space)
				return SpecialContentType.None;

			switch (left.Type)
			{
				//Fingerings nur kombiniert werden, wenn dabei Fingerings oder Text herauskommen
				case ContentType.Fingering:
					return right.Type switch
					{
						ContentType.Punctuation or ContentType.Word or ContentType.Fingering => SpecialContentType.Fingering | SpecialContentType.Text,
						_ => SpecialContentType.None,
					};

				//Akkorde können nur kombiniert werden, wenn dabei Akkorde oder Text herauskommen
				case ContentType.Chord:
					return right.Type switch
					{
						ContentType.Word or ContentType.Punctuation or ContentType.Fingering => SpecialContentType.Chord | SpecialContentType.Text,
						_ => SpecialContentType.None,
					};

				//An Punctuations können nur Punctuations angehängt werden
				case ContentType.Punctuation:
					return right.Type == ContentType.Punctuation ? SpecialContentType.Text : SpecialContentType.None;

				//An Wörter kann alles außer Leerzeichen oder Punctuations angehängt werden
				case ContentType.Word:
					return right.Type switch
					{
						ContentType.Word or ContentType.Chord or ContentType.Fingering => SpecialContentType.Text | SpecialContentType.Chord | SpecialContentType.Fingering,
						_ => SpecialContentType.None,
					};

				default:
					return SpecialContentType.None;
			}
		}

		private static bool CanAppendTo(ComponentContent left, ComponentContent right, ComponentContent total)
		{
			//Zwei Akkorde oder ein Akkord und ein Fingering können nicht kombiniert werden
			if ((left.Type, right.Type) is (ContentType.Chord, ContentType.Fingering) or (ContentType.Fingering, ContentType.Chord) or (ContentType.Chord, ContentType.Chord))
				return false;

			//Fingerings können mit Punctionations kombiniert werden, wenn dabei Fingerings herauskommen
			if ((left.Type, right.Type) is (ContentType.Fingering, ContentType.Punctuation) or (ContentType.Punctuation, ContentType.Fingering))
				return total.Type == ContentType.Fingering;

			//Spaces oder Punctuations können nur untereinander kombiniert werden
			if (left.Type is ContentType.Space or ContentType.Punctuation)
				return right.Type == left.Type;
			if (right.Type is ContentType.Space or ContentType.Punctuation)
				return right.Type == left.Type;

			return true;
		}

		//=> left switch
		//{
		//	{ Type: ContentType.Word } when total is { Type: ContentType.Fingering } => true,
		//	{ Type: ContentType.Word } => right is { Type: ContentType.Word or ContentType.Chord },
		//	{ Type: ContentType.Chord } => right is { Type: ContentType.Word or ContentType.Chord },
		//	{ Type: ContentType.Space } => right is { Type: ContentType.Space },
		//	{ Type: ContentType.Punctuation } => right is { Type: ContentType.Punctuation },
		//	{ Type: ContentType.Fingering } => right is { Type: ContentType.Word or ContentType.Chord or ContentType.Punctuation or ContentType.Fingering },
		//	_ => false,
		//};
		#endregion

		#region Operators
		public string? ToString(ISheetFormatter? formatter) => Content.ToString(formatter);

		public override string? ToString() => Content.ToString();
		#endregion

		public abstract class Attachment : SheetLineComponent
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

			internal abstract SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter);
		}

		public sealed class VarietyAttachment : Attachment
		{
			internal ComponentContent Content { get; private set; }

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

			internal VarietyAttachment(ContentOffset offset, ComponentContent content)
				: base(offset)
			{
				Content = content;
			}

			public static VarietyAttachment FromString(ContentOffset offset, string content, ISheetEditorFormatter? formatter)
				=> new VarietyAttachment(offset, ComponentContent.FromString(content, formatter));

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return Content.CreateElement(new(this, Offset), formatter) with
				{
					Tags = [SheetDisplayTag.Attachment]
				};
			}

			private void SetRenderBounds(RenderBounds bounds)
				=> RenderBounds = bounds;

			#region Editing
			internal Action? TryRemoveContent(ContentOffset offset, ContentOffset length, ISheetEditorFormatter? formatter)
			{
				//Entferne den Inhalt
				var newContent = Content.RemoveContent(offset, length, SpecialContentType.All, formatter);
				if (newContent is null)
					return null;

				//Setze den neuen Inhalt
				return () => Content = newContent.Value;
			}

			internal void InsertContent(string content, ContentOffset offset, ISheetEditorFormatter? formatter)
			{
				//Füge Inhalt ein
				Content = Content.MergeContents(content, offset, SpecialContentType.All, formatter).NewContent;
			}

			internal void ReplaceContent(string content, ISheetEditorFormatter? formatter)
			{
				//Ersetze Inhalt
				Content = ComponentContent.FromString(content, formatter, SpecialContentType.All);
			}

			internal VarietyAttachment? SplitEnd(ContentOffset offset, ISheetEditorFormatter? formatter)
			{
				//Trenne den Inhalt
				var newContent = Content.SplitEnd(offset, SpecialContentType.All, SpecialContentType.All, formatter);
				if (newContent is null) return null;
				Content = newContent.Value.NewContent;

				//Erzeuge das neue Ende
				var newEnd = new VarietyAttachment(Offset + offset, newContent.Value.EndContent);
				return newEnd;
			}

			internal bool TryMergeContents(VarietyAttachment attachment, ISheetEditorFormatter? formatter)
			{
				//Füge Inhalt hinzu
				Content = Content.MergeContents(attachment.Content, ContentOffset.FarEnd, SpecialContentType.All, formatter).NewContent;
				return true;
			}
			#endregion
		}
	}
	#endregion
}
