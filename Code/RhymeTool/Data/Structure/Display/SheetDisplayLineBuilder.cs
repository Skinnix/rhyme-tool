namespace Skinnix.RhymeTool.Data.Structure.Display;

public abstract class SheetDisplayLineBuilder : IComparable<SheetDisplayLineBuilder>
{
	public abstract int CurrentLength { get; }

	public abstract int CompareTo(SheetDisplayLineBuilder? other);
	public abstract SheetDisplayLine CreateDisplayLine();

	public abstract void Append(SheetDisplayElement element);
	public abstract void ExtendLength(int totalLength);
}

public abstract class SheetDisplayLineBuilder<TLine> : SheetDisplayLineBuilder
	where TLine : SheetDisplayLine
{
	public abstract override TLine CreateDisplayLine();
}

public abstract class SheetDisplayTextLineBuilder<TLine> : SheetDisplayLineBuilder<TLine>
	where TLine : SheetDisplayLine
{
	protected List<SheetDisplayElement> Elements { get; } = new();

	private int currentLength;
	public override int CurrentLength => currentLength;

	public override void Append(SheetDisplayElement element)
	{
		var length = element.Length;
		Elements.Add(element);
		currentLength += length;
	}

	public override void ExtendLength(int totalLength)
	{
		if (currentLength >= totalLength) return;

		var space = new SheetDisplaySpace(totalLength - currentLength);
		Elements.Add(space);
		currentLength = totalLength;
	}
}