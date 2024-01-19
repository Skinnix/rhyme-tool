using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineElementSource;

public abstract record SheetDisplayLineElement(ISheetDisplayLineElementSource? Source)
{
    public abstract int GetLength(ISheetFormatter? formatter);

    public override string ToString() => ToString(formatter: null);
    public abstract string ToString(ISheetFormatter? formatter = null);
}

public record SheetDisplayLineVoid(ISheetDisplayLineElementSource? Source) : SheetDisplayLineElement(Source)
{
    public override int GetLength(ISheetFormatter? formatter) => 0;
    public override string ToString(ISheetFormatter? formatter = null) => string.Empty;
}

public record SheetDisplayLineSpace(ISheetDisplayLineElementSource? Source, int Count) : SheetDisplayLineElement(Source)
{
    public override int GetLength(ISheetFormatter? formatter) => Count;
    public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Count);
}

[Obsolete]
public record SheetDisplayLineText(ISheetDisplayLineElementSource Source, string Text) : SheetDisplayLineElement(Source)
{
    public override int GetLength(ISheetFormatter? formatter) => Text.Length;
    public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineAnchorText(ISheetDisplayLineElementSource Source, string Text) : SheetDisplayLineElement(Source)
{
    public IReadOnlyCollection<SheetDisplayLineElement> Targets { get; init; } = Array.Empty<SheetDisplayLineElement>();

    public override int GetLength(ISheetFormatter? formatter) => Text.Length;
    public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineChord(ISheetDisplayLineElementSource Source, Chord Chord) : SheetDisplayLineElement(Source)
{
    public override int GetLength(ISheetFormatter? formatter) => Chord.ToString(formatter).Length;
    public override string ToString(ISheetFormatter? formatter = null) => Chord.ToString(formatter);
}