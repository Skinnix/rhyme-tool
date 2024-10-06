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

public record SheetDisplayLineTabEmptyNote() : SheetDisplayLineTabElement
{
	public override string ToString(ISheetFormatter? formatter = null) => "-";
}

public record SheetDisplayLineTabBarLine() : SheetDisplayLineTabElement
{
	public override string ToString(ISheetFormatter? formatter = null) => "|";
}

public record SheetDisplayLineTabNote(string Text) : SheetDisplayLineTabElement
{
	public override int GetLength(ISheetFormatter? formatter) => 1;
	public override string ToString(ISheetFormatter? formatter = null) => Text;
}
