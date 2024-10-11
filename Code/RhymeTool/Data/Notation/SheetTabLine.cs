using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetTabLine : SheetLine
{
	public static readonly Reason CannotMixTabsWithOthers = new("Tabulaturen können nicht gleichzeitig mit anderen Zeilen bearbeitet werden");
	public static readonly Reason CannotMultilineEdit = new("Tabulaturzeilen können nicht mehrzeilig bearbeitet werden");
	public static readonly Reason CannotEditMultipleNotes = new("Mehrere Noten können nicht gleichzeitig bearbeitet werden");
	public static readonly Reason NotANumber = new("Der Inhalt muss eine Zahl sein");
	public static readonly Reason CannotEditBarLines = new("Taktstriche können nicht bearbeitet werden");
	public static readonly Reason InvalidPosition = new("Ungültige Position");

	private ISheetBuilderFormatter? cachedFormatter;
	private IEnumerable<SheetDisplayLine>? cachedLines;

	private List<int> barLineEditIndexes = [];

	public TabLineDefinition.Collection Lines { get; }
	public Component.Collection Components { get; }

	private int barLength = 8;
	public int BarLength
	{
		get => barLength;
		set
		{
			if (value == barLength)
				return;

			if (value < 1)
				throw new ArgumentOutOfRangeException(nameof(BarLength), "Mindestens eine Note pro Takt erforderlich");

			Set(ref barLength, value);
		}
	}

	public SheetTabLine()
	{
		Lines = new TabLineDefinition.Collection(this, [Note.E, Note.B, Note.G, Note.D, Note.A, Note.E]);
		Components = new Component.Collection(this);
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
		RaiseModified(new ModifiedEventArgs(this));
	}

	private IEnumerable<SheetDisplayLine> CreateDisplayLinesCore(ISheetBuilderFormatter? formatter)
	{
		var bars = (Components.Count + BarLength) / BarLength;
		if (bars < 1)
			bars = 1;
		var builder = new Builder(Lines, formatter, barLineEditIndexes = new());

		//Notenwerte
		var i = 0;
		foreach (var line in Lines)
		{
			builder.Builders[i++].Append(new SheetDisplayLineTabLineNote(line.Note)
			{
				Slice = null,
			}, formatter);
		}

		//Setze alle Zeilen auf die gleiche Länge
		var maxLength = builder.Builders.Max(b => b.CurrentLength);
		foreach (var line in builder.Builders)
		{
			if (line.CurrentLength < maxLength)
			{
				var space = new SheetDisplayTabLineFormatSpace(maxLength - line.CurrentLength)
				{
					Slice = null,
				};
				line.Append(space, formatter);
			}
		}

		//Erster Taktstrich
		builder.AddBarLine();

		//Spalten
		var index = -1;
		var currentBarLength = 0;
		foreach (var component in Components)
		{
			index++;

			//Neuer Takt?
			if (currentBarLength >= BarLength)
			{
				builder.AddBarLine();
				currentBarLength = 1;
			}
			else
			{
				currentBarLength++;
			}

			//Keine Komponente?
			if (component is null)
			{
				builder.AddEmpty();
				continue;
			}

			//Setze Render-Offset
			var renderOffset = builder.Builders[0].CurrentLength;
			component.RenderBounds = new(renderOffset, renderOffset + 1);

			//Füge Komponente hinzu
			i = -1;
			foreach (var line in builder.Builders)
			{
				i++;
				var note = component.TryGetNote(i);
				if (note.IsEmpty)
				{
					line.Append(new SheetDisplayLineTabEmptyNote()
					{
						Slice = new(component, new ContentOffset(index)),
					}, formatter);
				}
				else
				{
					line.Append(new SheetDisplayLineTabNote(note.Value.ToString()!)
					{
						Slice = new(component, new ContentOffset(index)),
					}, formatter);
				}
			}
		}

		//Mache den aktuellen Takt voll
		while (currentBarLength < BarLength)
		{
			builder.AddEmpty();
			currentBarLength++;
		}

		//Letzter Taktstrich
		builder.AddBarLine();

		//Erzeuge Zeilen
		return builder.CreateLines();
	}

	private readonly struct Builder
	{
		private readonly List<int> barLineIndexes;

		public SheetDisplayTabLine.Builder[] Builders { get; }
		public IReadOnlyList<TabLineDefinition> Definitions { get; }
		public ISheetBuilderFormatter? Formatter { get; }

		public Builder(IReadOnlyList<TabLineDefinition> definitions, ISheetBuilderFormatter? formatter, List<int> barLineIndexes)
		{
			Builders = new SheetDisplayTabLine.Builder[definitions.Count];
			Definitions = definitions;
			for (var i = 0; i < Builders.Length; i++)
				Builders[i] = new SheetDisplayTabLine.Builder();
			Formatter = formatter;
			this.barLineIndexes = barLineIndexes;
		}

		public void AddBarLine()
		{
			barLineIndexes.Add(Builders[0].CurrentLength);

			foreach (var line in Builders)
				line.Append(new SheetDisplayLineTabBarLine()
				{
					Slice = null,
				}, Formatter);
		}

		public void AddEmpty()
		{
			foreach (var line in Builders)
				line.Append(new SheetDisplayLineTabEmptyNote()
				{
					Slice = null,
				}, Formatter);
		}

		public IEnumerable<SheetDisplayLine> CreateLines()
		{
			var i = 0;
			foreach (var definition in Definitions)
			{
				var builder = Builders[i];
				yield return builder.CreateDisplayLine(i, definition);
				i++;
			}
		}
	}
	#endregion

	public class TabLineDefinition(SheetTabLine owner, int lineIndex, Note note) : ISheetDisplayLineEditing
	{
		private const int INDEX_BAR_LINE = -1;
		private const int INDEX_INVALID = -2;

		private readonly int lineIndex = lineIndex;

		public SheetLine Line => owner;
		public int LineId => lineIndex;

		public Note Note { get; } = note;

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
				return NoEdit(context);

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
			if (range.Length == 0)
			{
				if (direction == DeleteDirection.Backward)
					range = new(range.Start - 1, range.Start);
				else
					range = new(range.Start, range.Start + 1);
			}

			//Finde Komponenten
			var hasBarLine = false;
			var hasInvalid = false;
			var components = Enumerable.Range(range.Start, range.Length)
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
			if (range.Length == 1 && components.Count == 0)
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
				if (owner.barLineEditIndexes.Count != 0 && range.Start == owner.barLineEditIndexes[^1] + 1)
				{
					//Füge hier ein Null-Element ein
					var index = GetBarAndNoteIndex(range.Start);
					willChange = true;
					return new DelayedMetalineEditResult(() =>
					{
						owner.Components.EnsureCreated(index.BarIndex * owner.BarLength + index.NoteIndex);
						if (raiseEvent)
							owner.RaiseModifiedAndInvalidateCache();
						return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(range.Start)));
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
						return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(range.Start)));
					});
				}

				return NoEdit(context);
			}

			//Lösche die Noten
			return new(() =>
			{
				foreach (var component in components)
				{
					component.Component!.SetNote(lineIndex, default);
					if (component.Component.IsEmpty)
						owner.Components[component.BarIndex * owner.BarLength + component.NoteIndex] = null;
				}

				if (raiseEvent)
					owner.RaiseModifiedAndInvalidateCache();
				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(range.Start)));
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
					//Bewege den Cursor danach um eins nach rechts
					if (deleteResult.Success)
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

			//Keine Zahl?
			if (!int.TryParse(content, out var value))
				return DelayedMetalineEditResult.Fail(NotANumber);

			//Einzeilige Bearbeitung?
			if (multilineContext is null)
				return TryInsertContent(context, context.SelectionRange, value, [lineIndex], formatter);

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
				lineIndexes.Add(line.lineIndex);
				if (line == multilineContext.EndLine)
					break;
			}

			//Bearbeite alle betroffenen Zeilen
			return TryInsertContent(context, range, value, lineIndexes, formatter);

			//var lineResults = new List<DelayedMetalineEditResult>();
			//foreach (var line in owner.Lines.SkipWhile(l => l != multilineContext.StartLine))
			//{
			//	//Bereite die Bearbeitung vor
			//	var lineResult = line.TryInsertContent(context, range, value, false, formatter);
			//	if (!lineResult.Success)
			//		return lineResult;

			//	//Speichere die Bearbeitung
			//	lineResults.Add(lineResult);
			//	if (line == multilineContext.EndLine)
			//		break;
			//}

			////Keine Veränderung?
			//if (lineResults.Count == 0)
			//	return NoEdit(context);

			////Führe die Bearbeitungen aus
			//return new DelayedMetalineEditResult(() =>
			//{
			//	//Führe aus und speichere die erste Bearbeitung
			//	MetalineEditResult? firstResult = null;
			//	foreach (var lineResult in lineResults)
			//	{
			//		var result = lineResult.Execute!();
			//		firstResult ??= result;
			//	}

			//	//Modified-Event
			//	owner.RaiseModifiedAndInvalidateCache();
			//	return firstResult!;
			//});
		}

		private DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SimpleRange range,
			int value, List<int> lineIndexes, ISheetEditorFormatter? formatter = null)
		{
			//Leere Range?
			if (range.Length == 0)
				range = new(range.Start, range.Start + 1);

			//Taktstrich?
			var invalid = false;
			var barLine = false;
			for (var i = range.Start; i < range.End; i++)
			{
				var note = GetBarAndNoteIndex(i);
				if (note.NoteIndex == INDEX_INVALID)
				{
					invalid = true;
					break;
				}

				if (note.NoteIndex == INDEX_BAR_LINE)
					barLine = true;
			}
			if (invalid)
				return DelayedMetalineEditResult.Fail(InvalidPosition);
			if (barLine)
				return DelayedMetalineEditResult.Fail(CannotEditBarLines);

			//Füge Werte ein
			return new(() =>
			{
				//Finde Komponenten
				var components = Enumerable.Range(range.Start, range.Length)
					.Select(i => (EditIndex: i, Data: GetBarAndNoteIndex(i)))
					.ToList();

				foreach (var component in components)
				{
					//Keine Komponente?
					if (component.Data.Component is null)
					{
						//Erzeuge die Komponente
						var newComponent = new Component(Enumerable.Range(0, owner.Lines.Count)
							.Select(i => lineIndexes.Contains(i) ? new TabNote(value) : default))
						{
							RenderBounds = new(component.EditIndex, component.EditIndex + 1),
						};

						//Füge die Komponente hinzu
						owner.Components[component.Data.BarIndex * owner.BarLength + component.Data.NoteIndex] = newComponent;
					}
					else
					{
						//Bearbeite die Komponente
						foreach (var index in lineIndexes)
							component.Data.Component.SetNote(index, new TabNote(value));
					}
				}

				//Modified-Event
				owner.RaiseModifiedAndInvalidateCache();

				//Nächste Position auf Taktstrich?
				var nextPosition = range.Start + 1;
				while (GetBarAndNoteIndex(nextPosition).NoteIndex < 0)
					nextPosition++;

				//Mehrzeilige Selektion
				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(nextPosition)) with
				{
					EndLineId = lineIndexes[^1],
				});
			});
		}

		private static SimpleRange GetActualEditRange(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext)
		{
			if (multilineContext is null)
				return context.SelectionRange;

			//Wenn mehrere Zeilen bearbeitet werden, simuliere eine Box-Auswahl
			return new(multilineContext.SelectionStart, multilineContext.SelectionEnd);
		}

		private DelayedMetalineEditResult NoEdit(SheetDisplayLineEditingContext context)
			=> new DelayedMetalineEditResult(() => new MetalineEditResult(new MetalineSelectionRange(this, context.SelectionRange)));

		private (int BarIndex, int NoteIndex, Component? Component) GetBarAndNoteIndex(int editIndex)
		{
			Component? last = null;
			int lastIndex = -1;
			var index = -1;
			foreach (var component in owner.Components)
			{
				//Vor dem editIndex?
				index++;
				if (component is null || component.RenderBounds.AfterOffset <= editIndex)
				{
					if (component is not null)
					{
						last = component;
						lastIndex = index;
					}
					continue;
				}

				//Taktindex und Notenindex berechnen
				var barNoteIndex = Math.DivRem(index, owner.BarLength);

				//Genau den Taktstrich getroffen?
				if (barNoteIndex.Remainder == 0 && owner.barLineEditIndexes.Contains(editIndex))
					return (barNoteIndex.Quotient, INDEX_BAR_LINE, null);

				//Komponente an der aktuellen Position?
				if (component is not null && component.RenderBounds.StartOffset <= editIndex && component.RenderBounds.AfterOffset > editIndex)
					return (barNoteIndex.Quotient, barNoteIndex.Remainder, component);

				//Keine Komponente
				break;
			}

			//Keine Komponenten?
			if (last is null)
			{
				//Taktindex und Notenindex berechnen
				var barNoteIndex = owner.barLineEditIndexes.Count == 0 ? Math.DivRem(editIndex, owner.BarLength + 1)
					: Math.DivRem(editIndex - owner.barLineEditIndexes[0], owner.BarLength + 1);

				//Genau den Taktstrich getroffen?
				if (barNoteIndex.Remainder == 0 && owner.barLineEditIndexes.Contains(editIndex))
					return (barNoteIndex.Quotient, INDEX_BAR_LINE, null);

				//Ungültige Position?
				if (barNoteIndex.Remainder < 1)
					return (barNoteIndex.Quotient, INDEX_INVALID, null);

				//Keine Komponente
				return (barNoteIndex.Quotient, barNoteIndex.Remainder - 1, null);
			}

			//Berechne Abstand zur letzten Komponente
			var distance = editIndex - last.RenderBounds.AfterOffset + 1;

			//Taktindex und Notenindex der letzten Komponente berechnen
			var lastBarNoteIndex = Math.DivRem(lastIndex, owner.BarLength);

			//Wie viele Taktstriche liegen dazwischen?
			var barLinesInBetween = Math.DivRem(lastBarNoteIndex.Remainder + distance, owner.BarLength + 1).Quotient;
			distance -= barLinesInBetween;

			//Index
			var componentIndex = lastIndex + distance;
			var componentPosition = Math.DivRem(componentIndex, owner.BarLength);
			var barIndex = componentPosition.Quotient;
			var noteIndex = componentPosition.Remainder;

			//var adjust = Math.DivRem(lastBarNoteIndex.Remainder + distance, owner.BarLength + 1);
			//var barIndex = lastBarNoteIndex.Quotient + adjust.Quotient;
			//var noteIndex = adjust.Remainder;
			//if (noteIndex == owner.barLength)
			//{
			//	barIndex++;
			//	noteIndex = 0;
			//}
			
			//Genau den Taktstrich getroffen?
			if (owner.barLineEditIndexes.Contains(editIndex))
				return (barIndex, INDEX_BAR_LINE, null);

			//Index am Ende
			return (barIndex, noteIndex, null);
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

			public IEnumerator<TabLineDefinition> GetEnumerator() => lines.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}

	public readonly record struct TabNote
	{
		private readonly int value;
		
		public int? Value => value == 0 ? null : value - 1;

		[MemberNotNullWhen(false, nameof(Value))]
		public bool IsEmpty => value == 0;

		public TabNote(int? value)
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value), "Note kann nicht kleiner als Null sein");

			this.value = value.GetValueOrDefault(-1) + 1;
		}
	}

	public class Component : SheetLineComponent
	{
		private TabNote[] notes;

		public IReadOnlyList<TabNote> Notes => notes;

		internal RenderBounds RenderBounds { get; set; } = RenderBounds.Empty;

		public bool IsEmpty => Notes.All(n => n.IsEmpty);

		public Component(IEnumerable<TabNote> notes)
		{
			this.notes = notes.ToArray();
		}

		public TabNote TryGetNote(int line)
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

		public class Collection : IReadOnlyList<Component?>
		{
			private readonly SheetTabLine owner;
			private readonly List<Component?> components;

			public int Count => components.Count;

			public Component? this[int index]
			{
				get => components[index];
				set => SetComponent(index, value);
			}

			public Collection(SheetTabLine owner)
			{
				this.owner = owner;
				this.components = new List<Component?>();
			}

			public Collection(SheetTabLine owner, IEnumerable<Component?> components)
			{
				this.owner = owner;
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
				if (components.Count + 1>= index)
					return;

				SetComponent(index, null, true);
			}

			public IEnumerator<Component?> GetEnumerator() => components.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
