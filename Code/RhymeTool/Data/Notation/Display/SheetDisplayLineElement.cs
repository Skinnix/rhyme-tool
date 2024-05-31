using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineElementSource;

public abstract record SheetDisplayLineElement
{
    public abstract int GetLength(ISheetFormatter? formatter);

    public override string ToString() => ToString(formatter: null);
    public abstract string ToString(ISheetFormatter? formatter = null);
}

public record SheetDisplayLineVoid : SheetDisplayLineElement
{
    public override int GetLength(ISheetFormatter? formatter) => 0;
    public override string ToString(ISheetFormatter? formatter = null) => string.Empty;
}

public record SheetDisplayLineSpace(int Count) : SheetDisplayLineElement
{
    public override int GetLength(ISheetFormatter? formatter) => Count;
    public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Count);
}

public record SheetDisplayLineText(string Text) : SheetDisplayLineElement
{
    public override int GetLength(ISheetFormatter? formatter) => Text.Length;
    public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineAnchorText(string Text) : SheetDisplayLineElement
{
    public IReadOnlyCollection<SheetDisplayLineElement> Targets { get; init; } = Array.Empty<SheetDisplayLineElement>();

    public override int GetLength(ISheetFormatter? formatter) => Text.Length;
    public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineChord(Chord Chord) : SheetDisplayLineElement
{
    public override int GetLength(ISheetFormatter? formatter) => Chord.ToString(formatter).Length;
    public override string ToString(ISheetFormatter? formatter = null) => Chord.ToString(formatter);
}

public record SheetDisplayLineSegmentTitleText(string Text) : SheetDisplayLineElement
{
	public override int GetLength(ISheetFormatter? formatter) => Text.Length;
	public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineSegmentTitleBracket(string Text) : SheetDisplayLineElement
{
	public override int GetLength(ISheetFormatter? formatter) => Text.Length;
	public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineHyphen(int Length) : SheetDisplayLineElement
{
	public override int GetLength(ISheetFormatter? formatter) => Length;
	public override string ToString(ISheetFormatter? formatter = null) => "-".PadRight(Length);
}