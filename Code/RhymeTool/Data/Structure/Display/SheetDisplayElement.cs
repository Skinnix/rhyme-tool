namespace Skinnix.RhymeTool.Data.Structure.Display;

public abstract record SheetDisplayElement
{
	public abstract int Length { get; }

	public abstract override string ToString();
}

public record SheetDisplayVoid : SheetDisplayElement
{
	public static SheetDisplayVoid Instance { get; } = new();

	public override int Length => 0;
	public override string ToString() => string.Empty;
}

public record SheetDisplaySpace(int Count) : SheetDisplayElement
{
	public override int Length => Count;

	public override string ToString() => new string(' ', Count);
}

public record SheetDisplayText(string Text) : SheetDisplayElement
{
	public override int Length => Text.Length;

	public override string ToString() => Text;
}

public record SheetDisplayChord(Chord Chord) : SheetDisplayElement
{
	public override int Length => Chord.ToString().Length;

	public override string ToString() => Chord.ToString();
}