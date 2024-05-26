namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract class SheetDisplayLineBuilder : IComparable<SheetDisplayLineBuilder>
{
    public abstract int CurrentLength { get; }

    public abstract int CompareTo(SheetDisplayLineBuilder? other);
    public abstract SheetDisplayLine CreateDisplayLine(ISheetDisplayLineEditing editing);

    public abstract void Append(SheetDisplayLineElement element, ISheetFormatter? formatter = null);
    public abstract void ExtendLength(int totalLength, int minExtension);
}

public abstract class SheetDisplayLineBuilder<TLine> : SheetDisplayLineBuilder
    where TLine : SheetDisplayLine
{
    public abstract override TLine CreateDisplayLine(ISheetDisplayLineEditing editing);
}

public abstract class SheetDisplayTextLineBuilder<TLine> : SheetDisplayLineBuilder<TLine>
    where TLine : SheetDisplayLine
{
    protected List<SheetDisplayLineElement> Elements { get; } = new();

    private int currentLength;
    public override int CurrentLength => currentLength;

    public override void Append(SheetDisplayLineElement element, ISheetFormatter? formatter = null)
    {
        var length = element.GetLength(formatter);
        Elements.Add(element);
        currentLength += length;
    }

    public override void ExtendLength(int totalLength, int minExtension)
    {
        if (currentLength + minExtension > totalLength)
            totalLength = currentLength + minExtension;

        if (currentLength >= totalLength)
            return;

        var space = new SheetDisplayLineSpace(totalLength - currentLength);
        Elements.Add(space);
        currentLength = totalLength;
    }
}