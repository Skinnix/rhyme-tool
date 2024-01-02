namespace Skinnix.RhymeTool.Data.Structure.Display;

public abstract record SheetDisplayLine
{
	public abstract IEnumerable<SheetDisplayElement> GetElements();
}

public sealed record SheetDisplayEmptyLine : SheetDisplayLine
{
	public override IEnumerable<SheetDisplayElement> GetElements()
		=> Enumerable.Empty<SheetDisplayElement>();
}

public sealed record SheetDisplaySpacerLine(int Length) : SheetDisplayLine
{
	public override IEnumerable<SheetDisplayElement> GetElements()
	{
		yield return new SheetDisplaySpace(Length);
	}
}

public sealed record SheetDisplayTextLine : SheetDisplayLine
{
	private readonly SheetDisplayElement[] elements;

	public SheetDisplayTextLine(params SheetDisplayElement[] elements) : this((IEnumerable<SheetDisplayElement>)elements) { }
	public SheetDisplayTextLine(IEnumerable<SheetDisplayElement> elements)
	{
		this.elements = elements.ToArray();
	}

	public override IEnumerable<SheetDisplayElement> GetElements() => elements;

	public class Builder : SheetDisplayTextLineBuilder<SheetDisplayTextLine>
	{
		public override SheetDisplayTextLine CreateDisplayLine() => new(Elements);

		public override int CompareTo(SheetDisplayLineBuilder? other)
		{
			if (other is SheetDisplayTextLine.Builder)
				return 0;

			return 1;
		}
	}
}

public sealed record SheetDisplayChordLine : SheetDisplayLine
{
	private readonly SheetDisplayElement[] elements;

	public SheetDisplayChordLine(params SheetDisplayElement[] elements) : this((IEnumerable<SheetDisplayElement>)elements) { }
	public SheetDisplayChordLine(IEnumerable<SheetDisplayElement> elements)
	{
		this.elements = elements.ToArray();
	}

	public override IEnumerable<SheetDisplayElement> GetElements() => elements;

	public class Builder : SheetDisplayTextLineBuilder<SheetDisplayChordLine>
	{
		public override SheetDisplayChordLine CreateDisplayLine() => new(Elements);

		public override int CompareTo(SheetDisplayLineBuilder? other)
		{
			if (other is SheetDisplayTextLine.Builder)
				return -1;

			if (other is SheetDisplayChordLine.Builder)
				return 0;

			return 1;
		}
	}
}