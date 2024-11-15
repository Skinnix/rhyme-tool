using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public readonly record struct VirtualContentOffset(ContentOffset ContentOffset, int VirtualOffset);

public record struct SheetDisplaySliceInfo(SheetLineComponent Component, ContentOffset ContentOffset, bool IsVirtual = false);

public abstract record SheetDisplayLineElementBase
{
	public virtual bool IsSpace => false;

	public abstract string Text { get; }

	public required SheetDisplaySliceInfo? Slice { get; init; }

	public IReadOnlyCollection<SheetDisplayTag> Tags { get; init; } = Array.Empty<SheetDisplayTag>();

	public int DisplayOffset { get; internal set; }
	public int DisplayLength { get; internal set; }

	public override sealed string ToString() => Text;
}

public abstract record SheetDisplayLineElement : SheetDisplayLineElementBase
{
	public virtual int Length => Text.Length;
}

public record SheetDisplayLineVoid : SheetDisplayLineElement
{
	public override bool IsSpace => true;
	public override string Text => string.Empty;
	public override int Length => 0;
}

public record SheetDisplayLineBreakPoint(int BreakPointIndex, int StartingPointOffset) : SheetDisplayLineElement
{
	public override bool IsSpace => true;
	public override string Text => string.Empty;
	public override int Length => 0;
}

public abstract record SheetDisplayLineSpaceBase(int Length) : SheetDisplayLineElement
{
	public override bool IsSpace => true;
	public override string Text => new string(' ', Length);
	public override int Length { get; } = Length;
}

public record SheetDisplayLineSpace(int Length) : SheetDisplayLineSpaceBase(Length);

public record SheetDisplayLineFormatSpace : SheetDisplayLineSpaceBase
{
	[SetsRequiredMembers]
	public SheetDisplayLineFormatSpace(int length)
		: base(length)
	{
		Slice = null;
	}
}

public abstract record SheetDisplayLineTextBase(string Text) : SheetDisplayLineElement
{
	public override string Text { get; } = Text;
}

public record SheetDisplayLineText(string Text) : SheetDisplayLineTextBase(Text);

public record SheetDisplayLineAnchorText(string Text) : SheetDisplayLineTextBase(Text)
{
    public IReadOnlyCollection<SheetDisplayLineElement> Targets { get; init; } = [];
}

public record SheetDisplayLineChord(Chord Chord, Chord.ChordFormat Format) : SheetDisplayLineElement
{
	public override string Text => Format.ToString();
}

public record SheetDisplayLineFingering(Fingering Fingering, Fingering.FingeringFormat Format) : SheetDisplayLineElement
{
	public override string Text => Format.ToString();
}

public record SheetDisplayLineRhythmPattern(RhythmPattern Rhythm, RhythmPattern.RhythmPatternFormat Format) : SheetDisplayLineElement
{
	public override string Text => Format.ToString();
}

public record SheetDisplayLineSegmentTitleText(string Text) : SheetDisplayLineTextBase(Text);

public record SheetDisplayLineSegmentTitleBracket(string Text, bool IsTitleStart) : SheetDisplayLineTextBase(Text);

public record SheetDisplayLineHyphen(int Length) : SheetDisplayLineElement
{
	public override int Length { get; } = Length;
	public override string Text => "-".PadRight(Length);
}

