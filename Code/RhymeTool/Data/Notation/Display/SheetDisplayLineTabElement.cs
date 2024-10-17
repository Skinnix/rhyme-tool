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

public abstract record SheetDisplayLineTabNoteBase() : SheetDisplayLineTabElement
{
	public virtual int Width { get; internal set; } = 1;

	public override int GetLength(ISheetFormatter? formatter) => Width;
}

public record SheetDisplayLineTabEmptyNote() : SheetDisplayLineTabNoteBase
{
	public override string ToString(ISheetFormatter? formatter = null)
	{
		if (Width == 1)
			return "-";

		var widthAfter = Width / 2;
		var widthBefore = Width - widthAfter - 1;
		return new string(' ', widthBefore) + "-" + new string(' ', widthAfter);
	}
}

public record SheetDisplayLineTabNote : SheetDisplayLineTabNoteBase
{
	public string Text { get; }

	public SheetDisplayLineTabNote(string text)
	{
		Text = text;
		Width = text.Length;
	}

	public override string ToString(ISheetFormatter? formatter = null) => Text;
}
