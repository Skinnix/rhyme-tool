using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

partial class SheetTabLine
{
	public class TabLineDefinition : ISheetDisplayLineEditing
	{
		private const int INDEX_INVALID = -1;
		private const int INDEX_BAR_LINE = -2;
		private const int INDEX_TUNING_NOTE = -3;

		private readonly SheetTabLine owner;

		internal int LineIndex { get; set; }

		public SheetLine Line => owner;
		public int LineId => LineIndex;

		public Note Tuning { get; set; }

		internal TabLineDefinition(SheetTabLine owner, int lineIndex, Note tuning)
		{
			this.owner = owner;
			LineIndex = lineIndex;
			Tuning = tuning;
		}

		public ReasonBase? SupportsEdit(SheetDisplayMultiLineEditingContext context)
		{
			if (context.StartLine is not ISheetDisplayLineEditing startLine
				|| context.EndLine is not ISheetDisplayLineEditing endLine)
				return CannotMixTabsWithOthers;

			if (context.LinesBetween.Count != 0)
				return CannotMultilineEdit;

			if (startLine.Line != endLine.Line)
				return CannotMultilineEdit;

			return null;
		}

		DelayedMetalineEditResult ISheetDisplayLineEditing.TryDeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter)
		{
			//Einzeilige Bearbeitung?
			if (multilineContext is null)
				return TryDeleteContent(context, context.SelectionRange, direction, type, true, out _, formatter);

			//Die StartLine kümmert sich um alles
			if (multilineContext.StartLine != this)
			{
				//Keine Bearbeitung und keine Auswahl nötig
				return new DelayedMetalineEditResult(MetalineEditResult.SuccessWithoutSelection);
			}

			//Lese tatsächliche Auswahl
			var range = GetActualEditRange(context, multilineContext);

			//Bearbeite alle betroffenen Zeilen
			var lineResults = new List<DelayedMetalineEditResult>();
			foreach (var line in owner.Lines.SkipWhile(l => l != multilineContext.StartLine))
			{
				//Bereite die Bearbeitung vor
				var lineResult = line.TryDeleteContent(context, range, direction, type, false, out var willChange, formatter);
				if (!lineResult.Success)
					return lineResult;

				//Keine Bearbeitung?
				if (!willChange)
					continue;

				//Speichere die Bearbeitung
				lineResults.Add(lineResult);
				if (line == multilineContext.EndLine)
					break;
			}

			//Keine Veränderung?
			if (lineResults.Count == 0)
				return NoEditMultiLine(multilineContext, range);

			//Führe die Bearbeitungen aus
			return new DelayedMetalineEditResult(() =>
			{
				//Führe aus und speichere die erste Bearbeitung
				MetalineEditResult? firstResult = null;
				foreach (var lineResult in lineResults)
				{
					var result = lineResult.Execute!();
					firstResult ??= result;
				}

				//Modified-Event
				owner.RaiseModifiedAndInvalidateCache();

				//Erfolg? (Safeguard)
				if (!firstResult!.Success)
					return firstResult;

				//Mehrzeilige Selektion
				return new MetalineEditResult(firstResult!.NewSelection with
				{
					EndLineId = multilineContext.EndLine.LineId,
				});
			});
		}
		private DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, SimpleRange range,
			DeleteDirection direction, DeleteType type, bool raiseEvent, out bool willChange, ISheetEditorFormatter? formatter = null)
		{
			//Erweitere leere Ranges in Richtung der Löschung
			var editRange = range;
			if (editRange.Length == 0)
			{
				//if (direction == DeleteDirection.Backward)
				//	editRange = new(editRange.Start - 1, editRange.Start);
				//else
				editRange = new(editRange.Start, editRange.Start + 1);
			}

			//Finde Komponenten
			var hasBarLine = false;
			var hasInvalid = false;
			var components = Enumerable.Range(editRange.Start, editRange.Length)
				.Select(GetBarAndNoteIndex)
				.Where(c =>
				{
					if (c.NoteIndex == INDEX_BAR_LINE)
						hasBarLine = true;
					else if (c.NoteIndex == INDEX_INVALID)
						hasInvalid = true;

					return c.Component is not null;
				})
				.ToList();

			//Nur Taktstrich/ungültig?
			willChange = components.Count != 0;
			if (editRange.Length == 1 && components.Count == 0)
			{
				if (hasInvalid)
					return DelayedMetalineEditResult.Fail(InvalidPosition);
				else if (hasBarLine)
					return DelayedMetalineEditResult.Fail(CannotEditBarLines);
			}

			//Nichts gefunden?
			if (!willChange)
			{
				//Sonderfall: wird nach dem letzten Taktstrich gelöscht?
				if (owner.barLineEditIndexes.Count != 0 && editRange.Start == owner.barLineEditIndexes[^1] + 1)
				{
					//Füge hier ein Null-Element ein
					var index = GetBarAndNoteIndex(editRange.Start);
					willChange = true;
					return new DelayedMetalineEditResult(() =>
					{
						owner.Components.EnsureCreated(index.BarIndex * owner.BarLength + index.NoteIndex);
						if (raiseEvent)
							owner.RaiseModifiedAndInvalidateCache();

						if (formatter?.KeepTabLineSelection == false)
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(editRange.Start)));
						else
							return new MetalineEditResult(new MetalineSelectionRange(this, range));
					});
				}

				//Sonderfall: wird nach dem obigen Szenario wieder irgendwo gelöscht?
				if (owner.Components.Count != 0 && owner.Components[^1] is null)
				{
					//Lösche die leeren Stellen am Ende wieder
					return new DelayedMetalineEditResult(() =>
					{
						owner.Components[owner.Components.Count - 1] = null;
						if (raiseEvent)
							owner.RaiseModifiedAndInvalidateCache();

						if (formatter?.KeepTabLineSelection == false)
							return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(editRange.Start)));
						else
							return new MetalineEditResult(new MetalineSelectionRange(this, range));
					});
				}

				return NoEditSingleLine(context);
			}

			//Lösche die Noten
			return new(() =>
			{
				foreach (var component in components)
				{
					component.Component!.SetNote(LineIndex, default);
					if (component.Component.IsEmpty)
						owner.Components[component.BarIndex * owner.BarLength + component.NoteIndex] = null;
				}

				if (raiseEvent)
					owner.RaiseModifiedAndInvalidateCache();

				if (formatter?.KeepTabLineSelection == false)
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(editRange.Start)));
				else
					return new MetalineEditResult(new MetalineSelectionRange(this, range));
			});
		}

		DelayedMetalineEditResult ISheetDisplayLineEditing.TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			string content, ISheetEditorFormatter? formatter)
		{
			//Minus?
			if (content == "-")
			{
				//Lösche das folgende Zeichen
				var deleteResult = (this as ISheetDisplayLineEditing).TryDeleteContent(context, multilineContext, DeleteDirection.Forward, DeleteType.Character);

				//Keine Auswahl?
				if (context.SelectionRange.Length == 0)
				{
					//Bewege den Cursor danach ggf. um eins nach rechts
					if (deleteResult.Success && formatter?.KeepTabLineSelection == false)
					{
						return new DelayedMetalineEditResult(() =>
						{
							var result = deleteResult.Execute();
							if (result.Success && result.NewSelection is not null)
							{
								//Nächste Position auf Taktstrich?
								var nextPosition = result.NewSelection.Range.Start + 1;
								while (GetBarAndNoteIndex(nextPosition).NoteIndex < 0)
									nextPosition++;
								return result with
								{
									NewSelection = result.NewSelection with
									{
										Range = SimpleRange.CursorAt(nextPosition)
									}
								};
							}

							return result;
						});
					}
				}

				return deleteResult;
			}

			//Einzeilige Bearbeitung?
			if (multilineContext is null)
				return TryInsertContent(context, context.SelectionRange, content, [LineIndex], formatter);

			//Die StartLine kümmert sich um alles
			if (multilineContext.StartLine != this)
			{
				//Keine Bearbeitung und keine Auswahl nötig
				return new DelayedMetalineEditResult(MetalineEditResult.SuccessWithoutSelection);
			}

			//Lese tatsächliche Auswahl
			var range = GetActualEditRange(context, multilineContext);

			//Sammle Indizes der betroffenen Zeilen
			var lineIndexes = new List<int>();
			foreach (var line in owner.Lines.SkipWhile(l => l != multilineContext.StartLine))
			{
				lineIndexes.Add(line.LineIndex);
				if (line == multilineContext.EndLine)
					break;
			}

			//Bearbeite alle betroffenen Zeilen
			return TryInsertContent(context, range, content, lineIndexes, formatter);
		}

		private DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SimpleRange range,
			string content, List<int> lineIndexes, ISheetEditorFormatter? formatter = null)
		{
			//Leere Range?
			var editRange = range;
			if (editRange.Length == 0)
				editRange = new(editRange.Start, editRange.Start + 1);

			//Taktstrich?
			var invalid = false;
			var barLine = false;
			var tuningNote = false;
			var tabNote = false;
			for (var i = editRange.Start; i < editRange.End; i++)
			{
				var notePosition = GetBarAndNoteIndex(i);
				if (notePosition.NoteIndex == INDEX_INVALID)
				{
					invalid = true;
					break;
				}

				if (notePosition.NoteIndex == INDEX_BAR_LINE)
					barLine = true;
				else if (notePosition.NoteIndex == INDEX_TUNING_NOTE)
					tuningNote = true;
				else
					tabNote = true;
			}
			if (invalid)
				return DelayedMetalineEditResult.Fail(InvalidPosition);
			if (barLine)
				return DelayedMetalineEditResult.Fail(CannotEditBarLines);

			//Stimmung und Noten (oder gar nichts)?
			if (tuningNote == tabNote)
				return DelayedMetalineEditResult.Fail(InvalidPosition);

			//Stimmung?
			if (tuningNote)
				return TryEditTuning(context, range, editRange, content, lineIndexes, formatter);

			//Lese Notenwert
			var isNote = int.TryParse(content, out var value);
			int? note = isNote ? value : null;
			var modifier = TabNoteModifier.None;
			if (!isNote)
			{
				var read = formatter?.TryReadTabNoteModifier(content, out modifier)
					?? EnumNameAttribute.TryRead(content, out modifier);
				if (read != content.Length)
					return DelayedMetalineEditResult.Fail(NotANumber);
			}

			//Füge Werte ein
			return new(() =>
			{
				//Finde Komponenten
				var components = Enumerable.Range(editRange.Start, editRange.Length)
					.Select(i => (EditIndex: i, Data: GetBarAndNoteIndex(i)))
					.ToList();

				foreach (var component in components)
				{
					//Keine Komponente?
					if (component.Data.Component is null)
					{
						//Erzeuge die Komponente
						var newComponent = new Component(Enumerable.Range(0, owner.Lines.Count)
							.Select(i => lineIndexes.Contains(i) ? new TabNote(note, modifier) : default));
						if (component.Data.ComponentIndex >= owner.indexBounds.Count)
							owner.indexBounds.Add(new(component.EditIndex, component.EditIndex + 1));
						else
							owner.indexBounds[component.Data.ComponentIndex] = new(component.EditIndex, component.EditIndex + 1);

						//Füge die Komponente hinzu
						owner.Components[component.Data.ComponentIndex] = newComponent;
					}
					else
					{
						//Für Zahlen > 10
						if (!context.JustSelected)
						{
							//Bearbeite die Komponente
							foreach (var index in lineIndexes)
							{
								var currentNote = component.Data.Component.GetNote(index);
								var noteValue = note;
								if (noteValue is not null)
								{
									if (currentNote.Value == 1)
										noteValue += 10;
									else if (currentNote.Value == 2)
										noteValue += 20;
								}

								var newNote = new TabNote(noteValue ?? currentNote.Value, currentNote.Modifier).TriggerModifier(modifier);
								component.Data.Component.SetNote(index, newNote);
							}
						}
						else
						{
							//Bearbeite die Komponente
							foreach (var index in lineIndexes)
							{
								var currentNote = component.Data.Component.GetNote(index);
								var newNote = new TabNote(note ?? currentNote.Value, currentNote.Modifier).TriggerModifier(modifier);
								component.Data.Component.SetNote(index, newNote);
							}
						}

						//Ist die Komponente jetzt leer?
						if (component.Data.Component.IsEmpty)
							owner.Components[component.Data.ComponentIndex] = null;
					}
				}

				//Modified-Event
				owner.RaiseModifiedAndInvalidateCache();

				//Kein Auto-Advance?
				if (formatter?.KeepTabLineSelection != false)
				{
					//Mehrzeilige Selektion
					return new MetalineEditResult(new MetalineSelectionRange(this, range) with
					{
						EndLineId = lineIndexes[^1],
					});
				}

				//Nächste Position auf Taktstrich?
				var nextPosition = editRange.Start + 1;
				while (GetBarAndNoteIndex(nextPosition).NoteIndex < 0)
					nextPosition++;

				//Mehrzeilige Selektion
				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(nextPosition)) with
				{
					EndLineId = lineIndexes[^1],
				});
			});
		}

		private DelayedMetalineEditResult TryEditTuning(SheetDisplayLineEditingContext context, SimpleRange range, SimpleRange editRange,
			string content, List<int> lineIndexes, ISheetEditorFormatter? formatter = null)
		{
			//Lese Note
			var read = Note.TryRead(content, out var note, formatter);
			if (read == content.Length)
			{
				return new(() =>
				{
					//Passe Tunings an
					foreach (var lineIndex in lineIndexes)
					{
						owner.Lines[lineIndex].Tuning = note;
					}

					owner.RaiseModifiedAndInvalidateCache();
					return new MetalineEditResult(new MetalineSelectionRange(this, range)
					{
						EndLineId = lineIndexes[^1],
					});
				});
			}

			//Lese Vorzeichen
			read = formatter?.TryReadAccidental(content, out var accidental) ?? EnumNameAttribute.TryRead(content, out accidental);
			if (read == content.Length)
			{
				//Formatiere die Töne
				var tunings = lineIndexes
					.Select(i => owner.Lines[i].Tuning)
					.Select(n => formatter?.Transformation?.TransformNote(n) ?? n)
					.Select(n => n.Accidental == accidental ? new Note(n.Type, AccidentalType.None) : new Note(n.Type, accidental))
					.Select(n => formatter?.Transformation?.UntransformNote(n) ?? n)
					.ToArray();

				return new(() =>
				{
					//Passe Tunings an
					foreach ((var lineIndex, var newTuning) in lineIndexes.Zip(tunings))
					{
						owner.Lines[lineIndex].Tuning = newTuning;
					}

					owner.RaiseModifiedAndInvalidateCache();
					return new MetalineEditResult(new MetalineSelectionRange(this, range)
					{
						EndLineId = lineIndexes[^1],
					});
				});
			}

			return DelayedMetalineEditResult.Fail(NotANote);
		}

		private static SimpleRange GetActualEditRange(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext)
		{
			if (multilineContext is null)
				return context.SelectionRange;

			//Wenn mehrere Zeilen bearbeitet werden, simuliere eine Box-Auswahl
			return new(multilineContext.SelectionStart, multilineContext.SelectionEnd);
		}

		private DelayedMetalineEditResult NoEditSingleLine(SheetDisplayLineEditingContext context)
			=> new DelayedMetalineEditResult(() => new MetalineEditResult(new MetalineSelectionRange(this, context.SelectionRange)));

		private DelayedMetalineEditResult NoEditMultiLine(SheetDisplayMultiLineEditingContext context, SimpleRange range)
			=> new DelayedMetalineEditResult(() => new MetalineEditResult(new MetalineSelectionRange(this, range)
			{
				EndLineId = context.EndLine.LineId,
			}));

		private (int ComponentIndex, int BarIndex, int NoteIndex, Component? Component) GetBarAndNoteIndex(int editIndex)
		{
			//Vor dem ersten Taktstrich?
			if (editIndex < owner.barLineEditIndexes[0])
				return (0, 0, INDEX_TUNING_NOTE, null);

			//Finde passende RenderBounds
			var index = owner.indexBounds.BinarySearch(new RenderBounds(editIndex, editIndex + 1), RenderBounds.PositionInsideComparer);
			if (index >= 0)
			{
				//Taktindex und Notenindex berechnen
				var barNoteIndex = Math.DivRem(index, owner.BarLength);
				return (index, barNoteIndex.Quotient, barNoteIndex.Remainder, owner.Components[index]);
			}

			//Taktstrich getroffen?
			var barLineIndex = owner.barLineEditIndexes.BinarySearch(editIndex);
			if (barLineIndex >= 0)
				return (0, barLineIndex - 1, INDEX_BAR_LINE, null);

			//Erste Note im nächsten Takt
			index = (owner.barLineEditIndexes.Count - 1) * owner.BarLength;
			return (index, owner.barLineEditIndexes.Count - 1, 0, null);
		}

		public class Collection : IReadOnlyList<TabLineDefinition>
		{
			private readonly SheetTabLine owner;
			private readonly List<TabLineDefinition> lines;

			public int Count => lines.Count;

			public TabLineDefinition this[int index] => lines[index];

			public Collection(SheetTabLine owner, IEnumerable<Note> lineNotes)
			{
				this.owner = owner;
				this.lines = new List<TabLineDefinition>(lineNotes.Select((n, i) => new TabLineDefinition(owner, i, n)));
			}

			public Collection(SheetTabLine owner, IEnumerable<TabLineDefinition> lines)
			{
				this.owner = owner;
				this.lines = new List<TabLineDefinition>(lines);
			}

			public TabLineDefinition Add(Note tuning)
			{
				var line = new TabLineDefinition(owner, lines.Count, tuning);
				lines.Add(line);
				return line;
			}

			public bool Remove(TabLineDefinition line)
			{
				if (!lines.Remove(line))
					return false;

				var i = 0;
				foreach (var currentLine in lines)
					currentLine.LineIndex = i++;

				return true;
			}

			public IEnumerator<TabLineDefinition> GetEnumerator() => lines.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public Stored Store() => new(this);

			public readonly struct Stored : ISelfStored<Stored, Collection, SheetTabLine>
			{
				private readonly TabLineDefinition.Stored[] lines;

				internal Stored(Collection collection)
				{
					lines = new TabLineDefinition.Stored[collection.Count];
					var i = 0;
					foreach (var line in collection)
						lines[i++] = line.Store();
				}

				public Collection Restore(SheetTabLine owner)
					=> new(owner, lines.Select((l, i) => l.Restore((owner, i))));

				public Stored OptimizeWith(Stored collection, out bool isEqual)
				{
					isEqual = collection.lines.Length == lines.Length
						&& collection.lines.SequenceEqual(lines);

					if (isEqual)
						return collection;

					return this;
				}
			}
		}

		public Stored Store() => new(this);

		public readonly record struct Stored : IStored<TabLineDefinition, (SheetTabLine owner, int lineIndex)>
		{
			private readonly Note tuning;

			internal Stored(TabLineDefinition line)
			{
				tuning = line.Tuning;
			}

			public TabLineDefinition Restore((SheetTabLine owner, int lineIndex) parameters)
				=> new(parameters.owner, parameters.lineIndex, tuning);
		}
	}

	public class Component : SheetLineComponent
	{
		private TabNote[] notes;

		public IReadOnlyList<TabNote> Notes => notes;

		public bool IsEmpty => Notes.All(n => n.IsEmpty);

		public Component(IEnumerable<TabNote> notes)
		{
			this.notes = notes.ToArray();
		}

		public TabNote GetNote(int line)
		{
			if (line < 0 || line >= notes.Length)
				return default;

			return notes[line];
		}

		public void SetNote(int line, TabNote note)
		{
			if (line < 0)
				throw new ArgumentOutOfRangeException(nameof(line), "Der Index darf nicht kleiner als Null sein");
			if (line >= notes.Length)
				Array.Resize(ref notes, line + 1);

			notes[line] = note;
		}

		public void TriggerModifier(int line, TabNoteModifier modifier)
		{
			if (line < 0)
				throw new ArgumentOutOfRangeException(nameof(line), "Der Index darf nicht kleiner als Null sein");
			if (line >= notes.Length)
				return;

			notes[line] = notes[line].TriggerModifier(modifier);
		}

		public class Collection : IReadOnlyList<Component?>
		{
			private readonly List<Component?> components;

			public int Count => components.Count;

			public Component? this[int index]
			{
				get => index >= components.Count ? null : components[index];
				set => SetComponent(index, value);
			}

			public Collection()
			{
				this.components = new List<Component?>();
			}

			public Collection(IEnumerable<Component?> components)
			{
				this.components = new List<Component?>(components);
			}

			private void SetComponent(int index, Component? component, bool forceNull = false)
			{
				if (component is not null || forceNull)
				{
					if (index >= components.Count)
					{
						while (components.Count < index)
							components.Add(null);
						components.Add(component);
					}
					else
					{
						components[index] = component;
					}
				}
				else
				{
					if (index >= components.Count)
					{
						if (components.Count != 0 && components[^1] is null)
							SetComponent(Count - 1, null);

						return;
					}

					if (index < components.Count - 1)
					{
						components[index] = null;
						if (components[^1] is null)
							SetComponent(Count - 1, null);
						return;
					}

					if (index == 0)
					{
						components.Clear();
						return;
					}

					var lastComponent = components.FindLastIndex(index - 1, c => c is not null);
					if (lastComponent == -1)
						components.Clear();
					else
						components.RemoveRange(lastComponent + 1, components.Count - lastComponent - 1);
				}
			}

			internal void EnsureCreated(int index)
			{
				if (components.Count + 1 >= index)
					return;

				SetComponent(index, null, true);
			}

			public IEnumerator<Component?> GetEnumerator() => components.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public Stored Store() => new(this);

			public readonly struct Stored : IStored<Collection>
			{
				private readonly Component.Stored?[] components;

				public Stored(Collection collection)
				{
					components = new Component.Stored?[collection.Count];
					for (var i = 0; i < collection.Count; i++)
						components[i] = collection[i]?.Store();
				}

				public Collection Restore()
					=> new(components.Select(c => c?.Restore()));

				public Stored OptimizeWith(Stored collection, out bool isEqual)
				{
					var newComponents = new Component.Stored?[components.Length];
					isEqual = components.Length == collection.components.Length;
					for (var i = 0; i < newComponents.Length; i++)
					{
						var component = components[i];
						if (component is null)
						{
							if (i >= collection.components.Length || collection.components[i] is not null)
								isEqual = false;

							continue;
						}

						if (i >= collection.components.Length || collection.components[i] is null)
						{
							isEqual = false;
							continue;
						}

						newComponents[i] = component!.Value.OptimizeWith(collection.components[i]!.Value, out var isComponentEqual);
						if (!isComponentEqual)
						{
							isEqual = false;
							newComponents[i] = newComponents[i]!.Value.OptimizeWith(collection.components);
						}
					}

					if (isEqual)
						return collection;

					return this;
				}
			}
		}

		public Stored Store() => new(this);

		public readonly record struct Stored : IStored<Component>
		{
			private readonly TabNote[] notes;

			internal Stored(Component component)
			{
				notes = (TabNote[])component.notes.Clone();
			}

			public Component Restore()
				=> new(notes);

			public Stored OptimizeWith(Stored component, out bool isEqual)
			{
				isEqual = component.notes.Length == notes.Length && component.notes.SequenceEqual(notes);
				if (isEqual)
					return component;

				return this;
			}

			public Stored OptimizeWith(IEnumerable<Stored?> components)
			{
				foreach (var component in components)
				{
					if (component is not null && component.Value.notes.Length == notes.Length && component.Value.notes.SequenceEqual(notes))
					{
						return component.Value;
					}
				}

				return this;
			}
		}
	}
}
