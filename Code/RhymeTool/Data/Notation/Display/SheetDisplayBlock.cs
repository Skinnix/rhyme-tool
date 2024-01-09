namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract record SheetDisplayBlock
{
    public abstract IEnumerable<SheetDisplayLine> GetLines();
}

//public sealed record SheetDisplaySpacerBlock(int Length) : SheetDisplayBlock
//{
//    public override IEnumerable<SheetDisplayLine> GetLines()
//    {
//        yield return new SheetDisplaySpacerLine(Length);
//    }
//}

public sealed record SheetDisplayContentBlock(IReadOnlyList<SheetDisplayLine> Lines) : SheetDisplayBlock
{
    public SheetDisplayContentBlock(params SheetDisplayLine[] lines) : this((IReadOnlyList<SheetDisplayLine>)lines) { }

    public override IEnumerable<SheetDisplayLine> GetLines() => Lines;
}