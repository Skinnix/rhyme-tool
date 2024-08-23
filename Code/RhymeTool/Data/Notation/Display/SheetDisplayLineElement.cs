using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public readonly record struct VirtualContentOffset(ContentOffset ContentOffset, int VirtualOffset);

public record struct SheetDisplaySliceInfo(int ComponentIndex, ContentOffset ContentOffset, bool IsVirtual = false);

public abstract record SheetDisplayLineElementBase
{
	public virtual bool IsSpace => false;

	public required SheetDisplaySliceInfo? Slice { get; init; }

	public IReadOnlyCollection<SheetDisplayTag> Tags { get; init; } = Array.Empty<SheetDisplayTag>();

	public int DisplayOffset { get; internal set; }
	public int DisplayLength { get; internal set; }

	public override string ToString() => ToString(formatter: null);
	public abstract string ToString(ISheetFormatter? formatter = null);
}

public abstract record SheetDisplayLineElement : SheetDisplayLineElementBase
{
    public virtual int GetLength(ISheetFormatter? formatter)
		=> ToString(formatter).Length;
}

public record SheetDisplayLineVoid : SheetDisplayLineElement
{
	public override bool IsSpace => true;

	public override int GetLength(ISheetFormatter? formatter) => 0;
    public override string ToString(ISheetFormatter? formatter = null) => string.Empty;
}

public record SheetDisplayLineBreakPoint(int BreakPointIndex, int StartingPointOffset) : SheetDisplayLineElement
{
	public override bool IsSpace => true;

	public override int GetLength(ISheetFormatter? formatter) => 0;
	public override string ToString(ISheetFormatter? formatter = null) => string.Empty;
}

public record SheetDisplayLineSpace(int Count) : SheetDisplayLineElement
{
	public override bool IsSpace => true;

	public override int GetLength(ISheetFormatter? formatter) => Count;
    public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Count);
}

public record SheetDisplayLineFormatSpace : SheetDisplayLineElement
{
	public override bool IsSpace => true;
	public int Count { get; init; }

	[SetsRequiredMembers]
	public SheetDisplayLineFormatSpace(int count)
	{
		Count = count;

		Slice = null;
	}

	public override int GetLength(ISheetFormatter? formatter) => Count;
	public override string ToString(ISheetFormatter? formatter = null) => new string(' ', Count);
}

public record SheetDisplayLineText(string Text) : SheetDisplayLineElement
{
    public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineAnchorText(string Text) : SheetDisplayLineElement
{
    public IReadOnlyCollection<SheetDisplayLineElement> Targets { get; init; } = Array.Empty<SheetDisplayLineElement>();

    public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineChord(Chord Chord) : SheetDisplayLineElement
{
    public override string ToString(ISheetFormatter? formatter = null) => Chord.ToString(formatter);
}

public record SheetDisplayLineFingering(Fingering Fingering) : SheetDisplayLineElement
{
    public override string ToString(ISheetFormatter? formatter = null) => Fingering.ToString(formatter);
}

public record SheetDisplayLineSegmentTitleText(string Text) : SheetDisplayLineElement
{
	public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineSegmentTitleBracket(string Text, bool IsTitleStart) : SheetDisplayLineElement
{
	public override string ToString(ISheetFormatter? formatter = null) => Text;
}

public record SheetDisplayLineHyphen(int Length) : SheetDisplayLineElement
{
	public override int GetLength(ISheetFormatter? formatter) => Length;
	public override string ToString(ISheetFormatter? formatter = null) => "-".PadRight(Length);
}