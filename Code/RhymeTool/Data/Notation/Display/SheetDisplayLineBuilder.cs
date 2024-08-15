using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract class SheetDisplayLineBuilder : IComparable<SheetDisplayLineBuilder>
{
    public abstract int CurrentLength { get; }
	public abstract int CurrentNonSpaceLength { get; }

    public abstract int CompareTo(SheetDisplayLineBuilder? other);
    public abstract SheetDisplayLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing);

    public abstract void Append(SheetDisplayLineElement element, ISheetFormatter? formatter = null);
	public abstract void EnsureSpaceBefore(int spaceBefore, ISheetFormatter? formatter = null);
    public abstract void ExtendLength(int totalLength, int minExtension, ISheetFormatter? formatter = null);
}

public abstract class SheetDisplayLineBuilder<TLine> : SheetDisplayLineBuilder
    where TLine : SheetDisplayLine
{
    public abstract override TLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing);
}

public abstract class SheetDisplayTextLineBuilder<TLine> : SheetDisplayLineBuilder<TLine>
    where TLine : SheetDisplayLine
{
	private readonly List<SheetDisplayLineElement> elements = new();
	protected IReadOnlyList<SheetDisplayLineElement> Elements => elements;

    private int currentLength;
    public override int CurrentLength => currentLength;

	private int currentNonSpaceLength;
	public override int CurrentNonSpaceLength => currentNonSpaceLength;

    public override void Append(SheetDisplayLineElement element, ISheetFormatter? formatter = null)
    {
        var length = element.GetLength(formatter);
        elements.Add(element);
		element.DisplayOffset = currentLength;
		element.DisplayLength = length;

        currentLength += length;
		if (!element.IsSpace)
			currentNonSpaceLength += length;
    }

	public override void EnsureSpaceBefore(int spaceBefore, ISheetFormatter? formatter = null)
	{
		var currentSpace = currentLength - currentNonSpaceLength;
		var extraSpace = spaceBefore - currentSpace;
		if (extraSpace <= 0)
			return;

		ExtendLength(0, extraSpace, formatter);
	}

	public override void ExtendLength(int totalLength, int minExtension, ISheetFormatter? formatter = null)
    {
		if (minExtension < 0) minExtension = 0;
		if (totalLength < currentLength) totalLength = currentLength;

        if (currentLength + minExtension > totalLength)
            totalLength = currentLength + minExtension;

        if (currentLength >= totalLength)
            return;

		var length = totalLength - currentLength;
        var space = new SheetDisplayLineFormatSpace(length)
		{
			DisplayOffset = currentLength,
			DisplayLength = length,
		};
        elements.Add(space);
        currentLength = totalLength;
    }
}