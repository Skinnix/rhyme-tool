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
				currentBarLength = 0;
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
			=> TryDeleteContent(context, multilineContext, direction, type, 0, formatter);
		public DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext, 
			DeleteDirection direction, DeleteType type, int selectionOffset, ISheetEditorFormatter? formatter = null)
		{
			//Lese tatsächliche Auswahl
			var range = GetActualEditRange(context, multilineContext);
			if (range.Length > 0)
				return DelayedMetalineEditResult.Fail(CannotEditMultipleNotes);

			//Lese Position
			var editIndex = range.Start;
			if (direction == DeleteDirection.Backward)
				editIndex--;
			var (barIndex, noteIndex, component) = GetBarAndNoteIndex(editIndex);

			//Keine Komponente?
			if (component is null)
				return NoEdit(context);

			//Keine Note?
			var note = component.TryGetNote(lineIndex);
			if (note.IsEmpty)
				return NoEdit(context);

			//Lösche die Note
			return new(() =>
			{
				component.SetNote(lineIndex, default);
				if (component.IsEmpty)
					owner.Components[barIndex * owner.BarLength + noteIndex] = null;
				owner.RaiseModifiedAndInvalidateCache();

				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(editIndex + selectionOffset)));
			});
		}

		public DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
			string content, ISheetEditorFormatter? formatter = null)
		{
			//Minus?
			if (content == "-")
				return TryDeleteContent(context, multilineContext, DeleteDirection.Forward, DeleteType.Character, 1, formatter);

			//Lese tatsächliche Auswahl
			var range = GetActualEditRange(context, multilineContext);
			if (range.Length > 0)
				return DelayedMetalineEditResult.Fail(CannotEditMultipleNotes);

			//Keine Zahl?
			if (!int.TryParse(content, out var value))
				return DelayedMetalineEditResult.Fail(NotANumber);

			//Lese Position
			var (barIndex, noteIndex, component) = GetBarAndNoteIndex(range.Start);

			//Taktstrich?
			if (noteIndex < 0)
				return DelayedMetalineEditResult.Fail(CannotEditBarLines);

			//Keine Komponente?
			if (component is null)
			{
				//Erzeuge die Komponente
				return new(() =>
				{
					//Erzeuge die Komponente
					var newComponent = new Component(Enumerable.Range(0, owner.Lines.Count)
						.Select(i => i == lineIndex ? new TabNote(value) : default));

					//Füge die Komponente hinzu
					owner.Components[barIndex * owner.BarLength + noteIndex] = newComponent;
					owner.RaiseModifiedAndInvalidateCache();
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(range.Start + 1)));
				});
			}

			//Bearbeite die Komponente
			return new(() =>
			{
				//Bearbeite die Note
				component.SetNote(lineIndex, new TabNote(value));
				owner.RaiseModifiedAndInvalidateCache();
				return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAt(range.Start + 1)));
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
					return (barNoteIndex.Quotient, -1, null);

				//Komponente an der aktuellen Position?
				if (component is not null && component.RenderBounds.StartOffset <= editIndex && component.RenderBounds.AfterOffset > editIndex)
					return (barNoteIndex.Quotient, barNoteIndex.Remainder, component);
				
				//Keine Komponente
				return (barNoteIndex.Quotient, barNoteIndex.Remainder, null);
			}

			//Keine Komponenten?
			if (last is null)
			{
				//Taktindex und Notenindex berechnen
				var barNoteIndex = owner.barLineEditIndexes.Count == 0 ? Math.DivRem(editIndex, owner.BarLength + 1)
					: Math.DivRem(editIndex - owner.barLineEditIndexes[0], owner.BarLength + 1);

				//Genau den Taktstrich getroffen?
				if (barNoteIndex.Remainder == 0 && owner.barLineEditIndexes.Contains(editIndex))
					return (barNoteIndex.Quotient, -1, null);

				//Keine Komponente
				return (barNoteIndex.Quotient, barNoteIndex.Remainder - 1, null);
			}

			//Berechne Abstand zur letzten Komponente
			var distance = editIndex - last.RenderBounds.AfterOffset + 1;

			//Taktindex und Notenindex der letzten Komponente berechnen
			var lastBarNoteIndex = Math.DivRem(lastIndex, owner.BarLength);
			var adjust = Math.DivRem(lastBarNoteIndex.Remainder + distance, owner.BarLength + 1);
			var barIndex = lastBarNoteIndex.Quotient + adjust.Quotient;
			var noteIndex = adjust.Remainder;
			if (noteIndex == owner.barLength)
			{
				barIndex++;
				noteIndex = 0;
			}

			//Genau den Taktstrich getroffen?
			if (noteIndex == 0 && owner.barLineEditIndexes.Contains(editIndex))
				return (barIndex, -1, null);

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

			private void SetComponent(int index, Component? component)
			{
				if (component is not null)
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
						return;

					if (index < components.Count - 1)
					{
						components[index] = null;
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

			public IEnumerator<Component?> GetEnumerator() => components.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
