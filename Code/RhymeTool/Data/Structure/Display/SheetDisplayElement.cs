namespace Skinnix.RhymeTool.Data.Structure.Display;

public abstract record SheetDisplayElement
{
	public abstract int GetLength(ISheetFormatter? formatter = null);

	public override string ToString() => ToString(formatter: null);
	public abstract string ToString(ISheetFormatter? formatter = null);
}

public record SheetDisplayVoid : SheetDisplayElement
{
	public static SheetDisplayVoid Instance { get; } = new();

	public override int GetLength(ISheetFormatter? formatter = null) => 0;
	public override string ToString(ISheetFormatter? formatter = null) => string.Empty;
}

public record SheetDisplaySpace(int Count) : SheetDisplayElement
{
	public override int GetLength(ISheetFormatter? formatter = null) => Count;

	public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Count);
}

public record SheetDisplayText(string Text) : SheetDisplayElement
{
	public override int GetLength(ISheetFormatter? formatter = null) => Text.Length;

	public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayChord(Chord Chord) : SheetDisplayElement
{
	public override int GetLength(ISheetFormatter? formatter = null) => Chord.ToString(formatter).Length;

	public override string ToString(ISheetFormatter? formatter = null) => Chord.ToString(formatter);
}

public record SheetDisplayAnchorText(string Text) : SheetDisplayElement
{
	public IReadOnlyCollection<SheetDisplayElement> Targets { get; init; } = Array.Empty<SheetDisplayElement>();

	public override int GetLength(ISheetFormatter? formatter = null) => Text.Length;

	public override string ToString(ISheetFormatter? formatter = null) => Text;
}