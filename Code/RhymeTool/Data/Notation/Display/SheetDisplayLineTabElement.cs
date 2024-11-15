namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract record SheetDisplayLineTabElement : SheetDisplayLineElement
{
}

public record SheetDisplayTabLineFormatSpace(int Length) : SheetDisplayLineTabElement
{
	public override int Length { get; } = Length;
	public override string Text => new string(' ', Length);
}

public record SheetDisplayLineTabBarLine() : SheetDisplayLineTabElement
{
	public override int Length => 1;
	public override string Text => "|";
}

public abstract record SheetDisplayLineWidthElement : SheetDisplayLineTabElement
{
	public virtual TabColumnWidth Width { get; internal set; }

	protected SheetDisplayLineWidthElement(TabColumnWidth width)
	{
		Width = width;
	}

	public abstract TabNote.SimpleTabNoteFormat GetFormat();
}

public record SheetDisplayLineTabTuning(Note Tuning, TabNote.TabNoteTuningFormat Format, TabColumnWidth Width) :
	SheetDisplayLineWidthElement(Width)
{
	public override string Text => Format.ToString();

	public override TabNote.SimpleTabNoteFormat GetFormat() => Format;
}

public abstract record SheetDisplayLineTabNoteBase(TabNote Note, TabNote.TabNoteFormat Format, TabColumnWidth Width) :
	SheetDisplayLineWidthElement(Width)
{
	public override string Text => Format.ToString();

	public override TabNote.SimpleTabNoteFormat GetFormat() => Format;
}

public record SheetDisplayLineTabEmptyNote(TabNote.TabNoteFormat Format, TabColumnWidth Width) :
	SheetDisplayLineTabNoteBase(TabNote.Empty, Format, Width);

public record SheetDisplayLineTabNote(TabNote Note, TabNote.TabNoteFormat Format, TabColumnWidth Width) :
	SheetDisplayLineTabNoteBase(Note, Format, Width);
