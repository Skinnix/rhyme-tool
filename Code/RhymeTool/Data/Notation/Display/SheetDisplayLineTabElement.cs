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
	public virtual int Width { get; internal set; } = 1;

	protected SheetDisplayLineWidthElement(int width)
	{
		Width = width;
	}

	public override int GetLength(ISheetFormatter? formatter) => Format(formatter).Text.Length;

	public abstract TabNoteFormat Format(ISheetFormatter? formatter);
}

public record SheetDisplayLineTabTuning : SheetDisplayLineWidthElement
{
	public Note Tuning { get; }

	public SheetDisplayLineTabTuning(Note tuning)
		: base(tuning.ToString().Length)
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
		if (Width <= tuningText.Length)
			return tuningText;

		var padding = Width - tuningText.Length;
		return tuningText + new string(' ', padding);
	}
}

public abstract record SheetDisplayLineTabNoteBase : SheetDisplayLineWidthElement
{
	public TabNote Note { get; }

	public SheetDisplayLineTabNoteBase(TabNote note)
		: base(note.ToString().Length)
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
		if (Width <= noteText.Length)
			return noteText;

		var padding = Width - noteText.Length;
		var paddingBefore = padding / 2;
		var paddingAfter = padding - paddingBefore;
		return new string(' ', paddingBefore) + noteText + new string(' ', paddingAfter);
	}
}

public record SheetDisplayLineTabEmptyNote() : SheetDisplayLineTabNoteBase(TabNote.Empty);

public record SheetDisplayLineTabNote(TabNote Note): SheetDisplayLineTabNoteBase(Note);
