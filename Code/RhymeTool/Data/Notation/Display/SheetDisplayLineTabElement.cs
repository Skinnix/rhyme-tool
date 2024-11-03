namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract record SheetDisplayLineTabElement : SheetDisplayLineElement
{
}

public record SheetDisplayTabLineFormatSpace(int Length) : SheetDisplayLineTabElement
{
	public override int GetLength(ISheetFormatter? formatter) => Length;
	public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Length);
}

public record SheetDisplayLineTabBarLine() : SheetDisplayLineTabElement
{
	public override int GetLength(ISheetFormatter? formatter) => 1;
	public override string ToString(ISheetFormatter? formatter = null) => "|";
}

public abstract record SheetDisplayLineWidthElement : SheetDisplayLineTabElement
{
	public virtual TabColumnWidth Width { get; internal set; }

	protected SheetDisplayLineWidthElement(TabColumnWidth width)
	{
		Width = width;
	}

	public override int GetLength(ISheetFormatter? formatter) => Format(formatter).Text.Length;

	public abstract TabNoteFormat Format(ISheetFormatter? formatter);
}

public record SheetDisplayLineTabTuning : SheetDisplayLineWidthElement
{
	public Note Tuning { get; }

	public SheetDisplayLineTabTuning(Note tuning, TabColumnWidth width)
		: base(width)
	{
		Tuning = tuning;
	}

	public override TabNoteFormat Format(ISheetFormatter? formatter)
		=> formatter?.Format(Tuning, Width)
		?? new TabNoteFormat(Tuning.ToString(), Width);

	public override string ToString(ISheetFormatter? formatter = null)
	{
		if (formatter is not null)
			return formatter.ToString(Tuning, Width);

		var tuningText = Tuning.ToString();
		if (Width.Max <= tuningText.Length)
			return tuningText;

		var padding = Width.Max - tuningText.Length;
		return tuningText + new string(' ', padding);
	}
}

public abstract record SheetDisplayLineTabNoteBase : SheetDisplayLineWidthElement
{
	public TabNote Note { get; }

	public SheetDisplayLineTabNoteBase(TabNote note, TabColumnWidth width)
		: base(width)
	{
		Note = note;
	}

	public override TabNoteFormat Format(ISheetFormatter? formatter)
		=> formatter?.Format(Note, Width)
		?? new TabNoteFormat(Note.ToString(), Width);

	public override string ToString(ISheetFormatter? formatter = null)
	{
		if (formatter is not null)
			return formatter.ToString(Note, Width);

		var noteText = Note.ToString();
		if (Width.Max <= noteText.Length)
			return noteText;

		var padding = Width.Max - noteText.Length;
		var paddingBefore = padding / 2;
		var paddingAfter = padding - paddingBefore;
		return new string(' ', paddingBefore) + noteText + new string(' ', paddingAfter);
	}
}

public record SheetDisplayLineTabEmptyNote(TabColumnWidth Width) : SheetDisplayLineTabNoteBase(TabNote.Empty, Width);

public record SheetDisplayLineTabNote(TabNote Note, TabColumnWidth Width) : SheetDisplayLineTabNoteBase(Note, Width);
