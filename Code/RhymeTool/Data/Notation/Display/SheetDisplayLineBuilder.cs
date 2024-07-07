using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract class SheetDisplayLineBuilder : IComparable<SheetDisplayLineBuilder>
{
    public abstract int CurrentLength { get; }

    public abstract int CompareTo(SheetDisplayLineBuilder? other);
    public abstract SheetDisplayLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing);

    public abstract void Append(SheetDisplayLineElement element, ISheetFormatter? formatter = null);
    public abstract void ExtendLength(int totalLength, int minExtension);

	public abstract Spacer AppendSpacer();

	public abstract record Spacer : SheetDisplayLineElement
	{
		public override int GetLength(ISheetFormatter? formatter) => 0;
		public override string ToString(ISheetFormatter? formatter = null) => string.Empty;

		public abstract void SetLength(int length);
	}
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

    public override void Append(SheetDisplayLineElement element, ISheetFormatter? formatter = null)
    {
        var length = element.GetLength(formatter);
        elements.Add(element);
		element.DisplayOffset = currentLength;
        currentLength += length;
    }

	public override Spacer AppendSpacer()
	{
		var spacer = new SpacerImpl(this);
		elements.Add(spacer);
		return spacer;
	}

	public override void ExtendLength(int totalLength, int minExtension)
    {
		if (minExtension < 0) minExtension = 0;
		if (totalLength < currentLength) totalLength = currentLength;

        if (currentLength + minExtension > totalLength)
            totalLength = currentLength + minExtension;

        if (currentLength >= totalLength)
            return;

        var space = new SheetDisplayLineFormatSpace(totalLength - currentLength)
		{
			DisplayOffset = currentLength
		};
        elements.Add(space);
        currentLength = totalLength;
    }

	private sealed record SpacerImpl : Spacer
	{
		private readonly SheetDisplayTextLineBuilder<TLine> owner;

		[SetsRequiredMembers]
		public SpacerImpl(SheetDisplayTextLineBuilder<TLine> owner)
		{
			this.owner = owner;

			Slice = null;
		}

		public override void SetLength(int length)
		{
			if (length == 0)
			{
				owner.elements.Remove(this);
			}
			else
			{
				owner.elements.Replace(this, new SheetDisplayLineFormatSpace(length));
				owner.currentLength += length;
			}
		}

		public bool Equals(SpacerImpl? other)
			=> ReferenceEquals(this, other);

		public override int GetHashCode() => 0;
	}
}