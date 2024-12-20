using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetTabLine : SheetLine, ISelectableSheetLine
{
	public static readonly Reason CannotMixTabsWithOthers = new("Tabulaturen k�nnen nicht gleichzeitig mit anderen Zeilen bearbeitet werden");
	public static readonly Reason CannotMultilineEdit = new("Tabulaturzeilen k�nnen nicht mehrzeilig bearbeitet werden");
	public static readonly Reason CannotEditMultipleNotes = new("Mehrere Noten k�nnen nicht gleichzeitig bearbeitet werden");
	public static readonly Reason NotANumber = new("Der Inhalt muss eine Zahl sein");
	public static readonly Reason NotANote = new("Der Inhalt muss eine Note sein");
	public static readonly Reason CannotEditBarLines = new("Taktstriche k�nnen nicht bearbeitet werden");
	public static readonly Reason InvalidPosition = new("Ung�ltige Position");

	public static SheetLineType LineType { get; } = SheetLineType.Create<SheetTabLine>("Tabulatur");

	private ISheetBuilderFormatter? cachedFormatter;
	private IEnumerable<SheetDisplayLine>? cachedLines;

	private List<int> barLineEditIndexes = [];
	private List<RenderBounds> indexBounds = [];

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

	public override bool IsEmpty
	{
		get
		{
			if (Components.Count == 0)
				return true;
			if (Components[^1] is not null)
				return false;

			return Components.All(c => c is null);
		}
	}

	public SheetTabLine()
		: base(LineType)
	{
		Lines = new TabLineDefinition.Collection(this, [Note.E, Note.B, Note.G, Note.D, Note.A, Note.E]);
		Components = new Component.Collection(this);
	}

	public SheetTabLine(IEnumerable<Note> tunings)
		: base(LineType)
	{
		Lines = new TabLineDefinition.Collection(this, tunings);
		Components = new Component.Collection(this);
	}

	#region Conversion
	public override IEnumerable<SheetLineConversion> GetPossibleConversions(ISheetBuilderFormatter? formatter = null)
	{
		if (!IsEmpty)
			return [];

		return [SheetLineConversion.Simple<SheetEmptyLine>.Instance];
	}
	#endregion

	#region Display
	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(SheetLineContext context, ISheetBuilderFormatter? formatter = null)
	{
		//Pr�fe Cache
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
		var totalNotes = bars * BarLength;
		indexBounds = new(totalNotes + 1);

		//Stimmung
		var i = 0;
		var tunings = (formatter ?? DefaultSheetFormatter.Instance).FormatAll(Lines.Select(l => l.Tuning));
		var displayTunings = tunings.Select(t => new SheetDisplayLineTabTuning(t.Note, t, t.Width)
		{
			Slice = null
		});

		//F�ge Tunings hinzu
		foreach ((var line, var tuning) in builder.Builders.Zip(displayTunings))
			line.Append(tuning, formatter);

		//Setze alle Zeilen auf die gleiche L�nge
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
				//Setze Render-Offset
				var currentRenderOffset = builder.Builders[0].CurrentLength;
				builder.AddEmpty();
				indexBounds.Add(new(currentRenderOffset, currentRenderOffset + 1));
				continue;
			}

			//Berechne Breite
			var componentWidth = formatter is null ? TabColumnWidth.Calculate(Enumerable.Range(0, Lines.Count).Select(i => component.GetNote(i).ToString()))
				: formatter.FormatAll(Enumerable.Range(0, Lines.Count).Select(component.GetNote))[0].Width;

			//Erzeuge Elemente
			var elements = new SheetDisplayLineTabNoteBase[Lines.Count];
			for (i = 0; i < Lines.Count; i++)
			{
				//Erzeuge Element
				var note = component.GetNote(i);
				var noteFormat = note.Format(componentWidth);
				var element = elements[i] = note.IsEmpty
					? new SheetDisplayLineTabEmptyNote(noteFormat, componentWidth)
					{
						Slice = new(component, new ContentOffset(index)),
					}
					: new SheetDisplayLineTabNote(note, noteFormat, componentWidth)
					{
						Slice = new(component, new ContentOffset(index)),
					};
			}

			//F�ge Elemente hinzu
			var renderOffset = builder.Builders[0].CurrentLength;
			foreach ((var line, var element) in builder.Builders.Zip(elements))
				line.Append(element, formatter);

			//Setze Render-Offset
			var afterOffset = builder.Builders[0].CurrentLength;
			indexBounds.Add(new(renderOffset, afterOffset));
		}

		//Mache den aktuellen Takt voll
		while (currentBarLength < BarLength)
		{
			index++;
			var renderOffset = builder.Builders[0].CurrentLength;
			builder.AddEmpty();
			currentBarLength++;

			//Setze Render-Offset
			indexBounds.Add(new(renderOffset, renderOffset + 1));
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
			var emptyNoteFormat = (Formatter ?? DefaultSheetFormatter.Instance).FormatAll([TabNote.Empty]);
			foreach (var line in Builders)
				line.Append(new SheetDisplayLineTabEmptyNote(emptyNoteFormat[0], emptyNoteFormat[0].Width)
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

	#region Definition/Editing
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

			//Die StartLine k�mmert sich um alles
			if (multilineContext.StartLine != this)
			{
				//Keine Bearbeitung und keine Auswahl n�tig
				return new DelayedMetalineEditResult(MetalineEditResult.SuccessWithoutSelection);
			}

			//Lese tats�chliche Auswahl
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

			//Keine Ver�nderung?
			if (lineResults.Count == 0)
				return NoEditMultiLine(multilineContext, range);

			//F�hre die Bearbeitungen aus
			return new DelayedMetalineEditResult(() =>
			{
				//F�hre aus und speichere die erste Bearbeitung
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
			//Erweitere leere Ranges in Richtung der L�schung
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

			//Nur Taktstrich/ung�ltig?
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
				//Sonderfall: wird nach dem letzten Taktstrich gel�scht?
				if (owner.barLineEditIndexes.Count != 0 && editRange.Start == owner.barLineEditIndexes[^1] + 1)
				{
					//F�ge hier ein Null-Element ein
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

				//Sonderfall: wird nach dem obigen Szenario wieder irgendwo gel�scht?
				if (owner.Components.Count != 0 && owner.Components[^1] is null)
				{
					//L�sche die leeren Stellen am Ende wieder
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

			//L�sche die Noten
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
				//L�sche das folgende Zeichen
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
								//N�chste Position auf Taktstrich?
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

			//Die StartLine k�mmert sich um alles
			if (multilineContext.StartLine != this)
			{
				//Keine Bearbeitung und keine Auswahl n�tig
				return new DelayedMetalineEditResult(MetalineEditResult.SuccessWithoutSelection);
			}

			//Lese tats�chliche Auswahl
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

			//F�ge Werte ein
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

						//F�ge die Komponente hinzu
						owner.Components[component.Data.ComponentIndex] = newComponent;
					}
					else
					{
						//F�r Zahlen > 10
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

				//N�chste Position auf Taktstrich?
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
				//Formatiere die T�ne
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

			//Erste Note im n�chsten Takt
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
		}
	}

	public class Component : SheetLineComponent
	{
		private TabNote[] notes;

		public IReadOnlyList<TabNote> Notes => notes;

		[Obsolete()]
		internal RenderBounds RenderBounds { get; set; } = RenderBounds.Empty;

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
			private readonly SheetTabLine owner;
			private readonly List<Component?> components;

			public int Count => components.Count;

			public Component? this[int index]
			{
				get => index >= components.Count ? null : components[index];
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
	#endregion
}
