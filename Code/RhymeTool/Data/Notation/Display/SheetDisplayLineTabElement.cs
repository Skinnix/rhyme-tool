namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract record SheetDisplayLineTabElement : SheetDisplayLineElement
{
	public override int GetLength(ISheetFormatter? formatter) => 1;
}

public record SheetDisplayLineTabLineNote(Note Note) : SheetDisplayLineTabElement
{
	public override string ToString(ISheetFormatter? formatter = null) => Note.ToString(formatter);
}

public record SheetDisplayTabLineFormatSpace(int Length) : SheetDisplayLineTabElement
{
	public override int GetLength(ISheetFormatter? formatter) => Length;
	public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Length);
}

public record SheetDisplayLineTabBarLine() : SheetDisplayLineTabElement
{
	public override string ToString(ISheetFormatter? formatter = null) => "|";
}

public abstract record SheetDisplayLineTabNoteBase : SheetDisplayLineTabElement
{
	public TabNote Note { get; }
	public virtual int Width { get; internal set; } = 1;

	public SheetDisplayLineTabNoteBase(TabNote note)
	{
		Note = note;
		Width = note.ToString().Length;
	}

	public override string ToString(ISheetFormatter? formatter = null)
	{
		if (formatter is not null)
			return formatter.ToString(Note, Width);

		var noteText = Note.ToString();
		if (Width <= noteText.Length)
			return noteText;

		var padding = Width - noteText.Length;
		var widthBefore = padding / 2;
		var widthAfter = padding - widthBefore;
		return new string(' ', widthBefore) + noteText + new string(' ', widthAfter);
	}
}

public record SheetDisplayLineTabEmptyNote() : SheetDisplayLineTabNoteBase(TabNote.Empty);

public record SheetDisplayLineTabNote(TabNote Note): SheetDisplayLineTabNoteBase(Note);
