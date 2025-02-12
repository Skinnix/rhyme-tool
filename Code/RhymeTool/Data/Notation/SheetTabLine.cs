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

public partial class SheetTabLine : SheetLine, ISelectableSheetLine
{
	public static readonly Reason CannotMixTabsWithOthers = new("Tabulaturen können nicht gleichzeitig mit anderen Zeilen bearbeitet werden");
	public static readonly Reason CannotMultilineEdit = new("Tabulaturzeilen können nicht mehrzeilig bearbeitet werden");
	public static readonly Reason CannotEditMultipleNotes = new("Mehrere Noten können nicht gleichzeitig bearbeitet werden");
	public static readonly Reason NotANumber = new("Der Inhalt muss eine Zahl sein");
	public static readonly Reason NotANote = new("Der Inhalt muss eine Note sein");
	public static readonly Reason CannotEditBarLines = new("Taktstriche können nicht bearbeitet werden");
	public static readonly Reason InvalidPosition = new("Ungültige Position");

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

			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(BarLength), "Keine negativen Taktlängen möglich");

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
		Components = new Component.Collection();
	}

	public SheetTabLine(IEnumerable<Note> tunings)
		: base(LineType)
	{
		Lines = new TabLineDefinition.Collection(this, tunings);
		Components = new Component.Collection();
	}

	private SheetTabLine(Func<SheetTabLine, TabLineDefinition.Collection> getLines, Func<Component.Collection> getComponents)
		: base(LineType)
	{
		Lines = getLines(this);
		Components = getComponents();
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
		var bars = BarLength == 0 ? 1 : (Components.Count + BarLength) / BarLength;
		if (bars < 1)
			bars = 1;
		var builder = new Builder(Lines, formatter, barLineEditIndexes = new());
		var totalNotes = BarLength == 0 ? Components.Count : bars * BarLength;
		indexBounds = new(totalNotes + 1);

		//Stimmung
		var i = 0;
		var tunings = (formatter ?? DefaultSheetFormatter.Instance).FormatAll(Lines.Select(l => l.Tuning));
		var displayTunings = tunings.Select(t => new SheetDisplayLineTabTuning(t.Note, t, t.Width)
		{
			Slice = null
		});

		//Füge Tunings hinzu
		foreach ((var line, var tuning) in builder.Builders.Zip(displayTunings))
			line.Append(tuning, formatter);

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
			if (BarLength != 0 && currentBarLength >= BarLength)
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
				var noteFormat = note.Format(componentWidth, formatter);
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

			//Füge Elemente hinzu
			var renderOffset = builder.Builders[0].CurrentLength;
			foreach ((var line, var element) in builder.Builders.Zip(elements))
				line.Append(element, formatter);

			//Setze Render-Offset
			var afterOffset = builder.Builders[0].CurrentLength;
			indexBounds.Add(new(renderOffset, afterOffset));
		}

		//Mache den aktuellen Takt voll
		while (BarLength != 0 && currentBarLength < BarLength)
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

	public override SheetLine.Stored Store() => new Stored(this);

	public sealed new class Stored : SheetLine.Stored, IStored<SheetTabLine>
	{
		private readonly Guid guid;
		private readonly TabLineDefinition.Collection.Stored lines;
		private readonly Component.Collection.Stored components;
		private readonly int barLength;

		internal Stored(SheetTabLine line)
			: this(line.Guid, line.Lines.Store(), line.Components.Store(), line.BarLength)
		{ }

		private Stored(Guid guid, TabLineDefinition.Collection.Stored lines, Component.Collection.Stored components, int barLength)
		{
			this.guid = guid;
			this.lines = lines;
			this.components = components;
			this.barLength = barLength;
		}

		public override SheetTabLine Restore()
			=> new SheetTabLine(lines.Restore, components.Restore)
			{
				Guid = guid,
				BarLength = barLength,
			};

		/*public override SheetLine.Stored OptimizeWith(IReadOnlyCollection<SheetLine.Stored> lines)
		{
			var match = lines.OfType<Stored>().FirstOrDefault(l => l.guid == guid);
			if (match is null)
				return this;

			return OptimizeWith(match);
		}

		private SheetLine.Stored OptimizeWith(Stored line)
		{
			var newLines = lines.OptimizeWith(line.lines, out var linesEqual);
			var newComponents = components.OptimizeWith(line.components, out var componentsEqual);

			if (barLength == line.barLength && linesEqual && componentsEqual)
				return line;

			if (barLength == line.barLength || linesEqual || componentsEqual)
				return new Stored(guid, lines, components, barLength);

			return this;
		}*/
	}
}
