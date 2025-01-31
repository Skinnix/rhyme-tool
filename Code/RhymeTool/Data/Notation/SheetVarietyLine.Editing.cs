using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

partial class SheetVarietyLine
{
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
			if (context.EffectiveRange.Length == 0)
			{
				//Wird der Zeilenumbruch am Anfang entfernt?
				if (context.EffectiveRange.Start == 0 && direction == DeleteDirection.Backward)
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
							varietyBefore.CreateDisplayLines(context.LineContext.Previous ?? throw new InvalidOperationException("Kontext nicht gefunden"), formatter);

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
					&& (Line.components.Count == 0 || context.EffectiveRange.End >= Line.components[^1].DisplayRenderBounds.EndOffset))
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
							Line.CreateDisplayLines(context.LineContext, formatter);

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
					if (component.DisplayRenderBounds.EndOffset == context.EffectiveRange.Start)
					{
						endComponent = component;
					}
					else if (component.DisplayRenderBounds.StartOffset == context.EffectiveRange.Start)
					{
						startComponent = component;
						nextComponent = component;
						break;
					}
					else if (component.DisplayRenderBounds.StartOffset > context.EffectiveRange.Start)
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
						context.EditRange = new SimpleRange(startComponent.DisplayRenderBounds.StartOffset, startComponent.DisplayRenderBounds.EndOffset);
					}

					//Steht der Cursor am Ende einer Komponente und soll diese gelöscht werden?
					else if (endComponent is not null && direction == DeleteDirection.Backward)
					{
						//Lösche die vorherige Komponente
						context.EditRange = new SimpleRange(endComponent.DisplayRenderBounds.StartOffset, endComponent.DisplayRenderBounds.EndOffset);
					}

					//Steht der Cursor mitten in einer Komponente?
					else if (previousComponent is not null)
					{
						//In welche Richtung soll gelöscht werden?
						if (direction == DeleteDirection.Forward)
						{
							//Kürze die Komponente nach dem Cursor
							context.EditRange = new SimpleRange(context.EffectiveRange.Start, previousComponent.DisplayRenderBounds.EndOffset);
						}
						else
						{
							//Kürze die Komponente vor dem Cursor
							context.EditRange = new SimpleRange(previousComponent.DisplayRenderBounds.StartOffset, context.EffectiveRange.Start);
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
							context.EditRange = new SimpleRange(endComponent.DisplayRenderBounds.EndOffset, nextComponent.DisplayRenderBounds.StartOffset + 1);
						}

						//Steht der Cursor mitten in einem langgestreckten Leerzeichen?
						else if (endComponent is null && previousComponent?.DisplayRenderBounds.EndOffset <= context.EffectiveRange.Start && nextComponent is not null
							&& (previousComponent as VarietyComponent)?.Content.Type == ContentType.Space)
						{
							//Lösche das erste Zeichen der ersten Komponente nach dem Leerzeichen
							context.EditRange = new SimpleRange(previousComponent.DisplayRenderBounds.EndOffset, nextComponent.DisplayRenderBounds.StartOffset + 1);
						}

						//Erweitere einfach die Auswahl um ein Zeichen nach rechts
						else
						{
							context.EditRange = new SimpleRange(context.EffectiveRange.Start, context.EffectiveRange.Start + 1);
						}
					}
					else
					{
						//Steht der Cursor am Anfang einer Komponente?
						if (startComponent is not null && previousComponent is not null)
						{
							//Lösche das letzte Zeichen der ersten Komponente davor
							context.EditRange = new SimpleRange(previousComponent.DisplayRenderBounds.EndOffset - 1, startComponent.DisplayRenderBounds.StartOffset);
						}

						//Steht der Cursor mitten in einem langgestreckten Leerzeichen?
						else if (startComponent is null && previousComponent?.DisplayRenderBounds.EndOffset < context.EffectiveRange.Start && prepreviousComponent is not null
							&& (previousComponent as VarietyComponent)?.Content.Type == ContentType.Space)
						{
							//Lösche das letzte Zeichen der ersten Komponente vor dem Leerzeichen
							context.EditRange = new SimpleRange(previousComponent.DisplayRenderBounds.StartOffset, prepreviousComponent.DisplayRenderBounds.EndOffset - 1);
						}

						//Erweitere einfach die Auswahl um ein Zeichen nach links
						else
						{
							context.EditRange = new SimpleRange(context.EffectiveRange.Start - 1, context.EffectiveRange.Start);
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
				if (context.EffectiveRange.Length > 0)
					return DelayedMetalineEditResult.Fail(CannotDeleteWithLinebreak);

				//Am Anfang?
				if (context.EffectiveRange.Start == 0 && context.EffectiveRange.End == 0)
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
						if (component.DisplayRenderBounds.EndOffset <= context.EffectiveRange.Start)
						{
							index++;
							continue;
						}

						//Liegt der Bereich in der Komponente?
						if (component.DisplayRenderBounds.StartOffset < context.EffectiveRange.Start)
						{
							//Teile die Komponente
							var splitOffset = component.DisplayRenderBounds.GetContentOffset(context.EffectiveRange.Start);
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
			var selectionRange = context.EffectiveRange;
			if (selectionRange.End == -1 && Line.components.Count != 0)
				selectionRange = new(selectionRange.Start, Line.components[^1].DisplayRenderBounds.EndOffset);

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
				if (component.DisplayRenderBounds.StartOffset < selectionRange.Start)
				{
					//Die Komponente ist der linke Rand
					leftEdge = component;
					leftEdgeIndex = index;
				}

				//Liegt die Komponente komplett vor dem Bereich?
				if (component.DisplayRenderBounds.EndOffset < selectionRange.Start)
					continue;

				//Beginnt die Komponente mit dem Bereich?
				if (component.DisplayRenderBounds.StartOffset == selectionRange.Start)
					rangeStartsOnComponent = true;

				//Endet die Komponente mit dem Bereich?
				if (component.DisplayRenderBounds.EndOffset == selectionRange.End)
					rangeEndsOnComponent = true;

				//Endet die Komponente nach dem Bereich?
				if (rightEdge is null && component.DisplayRenderBounds.EndOffset > selectionRange.End)
				{
					//Die Komponente ist der rechte Rand
					rightEdge = component;
					rightEdgeIndex = index;
				}

				//Liegt die Komponente komplett hinter dem Bereich?
				if (component.DisplayRenderBounds.StartOffset > selectionRange.End)
					break;

				//Liegt die Komponente komplett im Bereich?
				if (component != leftEdge && component != rightEdge
					&& component.DisplayRenderBounds.StartOffset >= selectionRange.Start && component.DisplayRenderBounds.EndOffset <= selectionRange.End)
				{
					//Die Komponente liegt im Bereich
					fullyInside.Add(component);
				}
			}

			//Prüfe auf Änderungen
			var removedAnything = fullyInside.Count != 0;
			var addedContent = false;
			var cursorPosition = selectionRange.Start;
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
					//cursorPosition = selectionRange.Start + newContentComponents[0].Content.GetLength(formatter);
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
					//cursorPosition = selectionRange.Start + newContentComponents[0].Content.GetLength(formatter);
					specialCursorPosition = SpecialCursorPosition.Behind(newContentComponents[0]);
				}
			}

			//Entferne Überlappung am linken Rand
			var skipTrimAfter = false;
			if (leftEdge is not null)
			{
				//Kürze die Komponente
				var leftEdgeOverlapOffset = leftEdge.DisplayRenderBounds.GetContentOffset(selectionRange.Start);
				var leftEdgeOverlapLength = leftEdge.DisplayRenderBounds.GetContentOffset(selectionRange.End).ContentOffset - leftEdgeOverlapOffset.ContentOffset;
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
								//cursorPosition = selectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
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
									//cursorPosition = selectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
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
				var rightEdgeOverlap = rightEdge.DisplayRenderBounds.GetContentOffset(selectionRange.End);
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
						//cursorPosition = selectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter));
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
				cursorPosition = selectionRange.Start + newContentComponents.Sum(c => c.Content.GetLength(formatter).Value);
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
				Line.CreateDisplayLines(context.LineContext, formatter);

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
			if (context.EffectiveRange.Length == 0)
			{
				//Wird ein Wort gelöscht?
				if (type == DeleteType.Word)
				{
					//Finde das nächste/vorherige Attachment
					var attachment = direction == DeleteDirection.Forward
						? FindAttachmentsInRange(SimpleRange.AllFromStart(context.EffectiveRange.Start), formatter).FirstOrDefault()
						: FindAttachmentsInRange(SimpleRange.AllToEnd(context.EffectiveRange.Start), formatter).LastOrDefault();

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
						Line.CreateDisplayLines(context.LineContext, formatter);

						//Setze den Cursor an die Stelle des gelöschten Attachments
						if (direction == DeleteDirection.Forward)
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(attachment.Attachment.RenderBounds.StartOffset)));
						else
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(attachment.Attachment.RenderBounds.AfterOffset)));
					});
				}

				//Erweitere einfach die Auswahl um ein Zeichen nach rechts oder links
				if (direction == DeleteDirection.Forward)
					context.EditRange = new SimpleRange(context.EffectiveRange.Start, context.EffectiveRange.Start + 1);
				else
					context.EditRange = new SimpleRange(context.EffectiveRange.Start - 1, context.EffectiveRange.Start);
			}

			//Finde alle Attachments im Bereich
			var attachments = FindAttachmentsInRange(context.EffectiveRange, formatter).ToList();

			//Liegt genau ein Attachment im Bereich?
			if (attachments.Count == 1)
			{
				//Bearbeite das Attachment
				return TryEditAttachment(context, attachments[0].Component, attachments[0].Attachment, context.EffectiveRange, null, formatter);
			}

			//Liegen mehrere Attachments im Bereich?
			if (attachments.Count > 1)
			{
				//Liegen sie nicht alle komplett im Bereich?
				if (!attachments.All(a => a.Attachment.RenderBounds.StartOffset >= context.EffectiveRange.Start && a.Attachment.RenderBounds.AfterOffset <= context.EffectiveRange.End))
					return DelayedMetalineEditResult.Fail(CannotPartiallyEditAttachments);

				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
					//Lösche die Attachments
					foreach (var (component, attachment) in attachments)
						component.RemoveAttachment(attachment);

					//Modified-Event
					Line.RaiseModifiedAndInvalidateCache();
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(context.EffectiveRange.Start)));
				});
			}

			//Finde das nächste Attachment und verschiebe es nach links
			return TryFindAndMoveNextAttachment(context.EffectiveRange, 0, formatter);
		}

		public DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			string content, ISheetEditorFormatter? formatter)
		{
			//Finde alle Attachments im Bereich
			var attachments = FindAttachmentsInRange(context.EffectiveRange, formatter).ToList();

			//Liegt genau ein Attachment im Bereich?
			if (attachments.Count == 1)
			{
				//Bearbeite das Attachment
				return TryEditAttachment(context, attachments[0].Component, attachments[0].Attachment, context.EffectiveRange, content, formatter);
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
				return TryFindAndMoveNextAttachment(context.EffectiveRange, content.Length, formatter);
			}

			//Ist der Bereich eine Selektion?
			if (context.EffectiveRange.Length > 0)
				return DelayedMetalineEditResult.Fail(CannotInsertAttachmentsIntoRange);

			//Finde die Komponente, in der der Inhalt eingefügt werden soll
			var component = Line.components.OfType<VarietyComponent>()
				.FirstOrDefault(c => c.DisplayRenderBounds.StartOffset <= context.EffectiveRange.Start && c.DisplayRenderBounds.EndOffset > context.EffectiveRange.End);
			if (component is null)
				return DelayedMetalineEditResult.Fail(NoComponentFoundHere);

			//Liegt ein Attachment direkt vor oder hinter der Position?
			var before = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().LastOrDefault(a => a.RenderBounds.AfterOffset == context.EffectiveRange.Start);
			var after = component.Attachments.OfType<VarietyComponent.VarietyAttachment>().FirstOrDefault(a => a.RenderBounds.StartOffset == context.EffectiveRange.End);
			if (before is not null)
			{
				//Bearbeitung wird funktionieren
				return new DelayedMetalineEditResult(() =>
				{
					//Füge den Inhalt ans Ende des Attachments an
					before.InsertContent(content, ContentOffset.FarEnd, formatter);

					//Erzeuge die Displayelemente der Zeile neu
					Line.RaiseModifiedAndInvalidateCache();
					Line.CreateDisplayLines(context.LineContext, formatter);

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
					Line.CreateDisplayLines(context.LineContext, formatter);

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
					var componentOffset = component.DisplayRenderBounds.GetContentOffset(context.EffectiveRange.Start);
					var newAttachment = VarietyComponent.VarietyAttachment.FromString(componentOffset.ContentOffset, content, formatter);
					component.AddAttachment(newAttachment);

					//Erzeuge die Displayelemente der Zeile neu
					Line.RaiseModifiedAndInvalidateCache();
					Line.CreateDisplayLines(context.LineContext, formatter);

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

		private DelayedMetalineEditResult TryEditAttachment(SheetDisplayLineEditingContext context, VarietyComponent component, VarietyComponent.VarietyAttachment attachment,
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
				Line.CreateDisplayLines(context.LineContext, formatter);

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
}
