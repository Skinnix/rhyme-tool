using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract class SheetDisplayLineBuilderBase : IComparable<SheetDisplayLineBuilderBase>
{
	public abstract Type LineType { get; }

    public abstract int CurrentLength { get; }
	public abstract int CurrentNonSpaceLength { get; }

    public abstract int CompareTo(SheetDisplayLineBuilderBase? other);
    public abstract SheetDisplayLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing);
}

public abstract class SheetDisplayLineBuilder<TElement, TLine> : SheetDisplayLineBuilderBase
	where TElement : SheetDisplayLineElement
    where TLine : SheetDisplayLine
{
	private readonly List<TElement> elements = new();
	protected IReadOnlyList<TElement> Elements => elements;

	private int currentLength;
	public override int CurrentLength => currentLength;

	private int currentNonSpaceLength;
	public override int CurrentNonSpaceLength => currentNonSpaceLength;

	public override Type LineType => typeof(TLine);

	public abstract override TLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing);

	public virtual void Append(TElement element, ISheetFormatter? formatter = null)
	{
		var length = element.GetLength(formatter);
		elements.Add(element);
		element.DisplayOffset = currentLength;
		element.DisplayLength = length;

		currentLength += length;
		if (!element.IsSpace)
			currentNonSpaceLength = currentLength;
	}
}

public abstract class SpacedSheetDisplayLineBuilder<TElement, TLine> : SheetDisplayLineBuilder<TElement, TLine>
	where TElement : SheetDisplayLineElement
	where TLine : SheetDisplayLine
{
	public abstract void EnsureSpaceBefore(int spaceBefore, ISheetFormatter? formatter = null);
	public abstract void ExtendLength(int totalLength, int minExtension, ISheetFormatter? formatter = null);
}

public abstract class SheetDisplayTextLineBuilder<TElement, TLine> : SpacedSheetDisplayLineBuilder<TElement, TLine>
	where TElement : SheetDisplayLineElement
	where TLine : SheetDisplayLine
{
	private readonly Func<SheetDisplayLineFormatSpace, TElement> createSpace;

	protected SheetDisplayTextLineBuilder(Func<SheetDisplayLineFormatSpace, TElement> createSpace)
	{
		this.createSpace = createSpace;
	}

	public override void EnsureSpaceBefore(int spaceBefore, ISheetFormatter? formatter = null)
	{
		var currentSpace = CurrentLength - CurrentNonSpaceLength;
		var extraSpace = spaceBefore - currentSpace;
		if (extraSpace <= 0)
			return;

		ExtendLength(0, extraSpace, formatter);
	}

	public override void ExtendLength(int totalLength, int minExtension, ISheetFormatter? formatter = null)
    {
		if (minExtension < 0) minExtension = 0;
		if (totalLength < CurrentLength) totalLength = CurrentLength;

        if (CurrentLength + minExtension > totalLength)
            totalLength = CurrentLength + minExtension;

        if (CurrentLength >= totalLength)
            return;

		var length = totalLength - CurrentLength;
        var space = new SheetDisplayLineFormatSpace(length)
		{
			DisplayOffset = CurrentLength,
			DisplayLength = length,
		};
		Append(createSpace(space), formatter);
    }
}