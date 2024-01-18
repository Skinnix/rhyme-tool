using System.Xml.Linq;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract record SheetDisplayLine
{
	public required ISheetDisplayLineEditing Editing { get; init; }

    public abstract IEnumerable<SheetDisplayLineElement> GetElements();

	public IEnumerable<(int Offset, SheetDisplayLineElement Element)> GetElementsIn(SimpleRange range, ISheetFormatter? formatter)
	{
		var currentOffset = 0;
		foreach (var element in GetElements())
		{
			//Berechne Länge des Elements
			var length = element.GetLength(formatter);

			//Berechne Offsets
			var startOffset = currentOffset;
			currentOffset += length;

			//Wenn das Element vor dem Start liegt, überspringe es
			if (currentOffset <= range.Start)
				continue;

			//Gib das Element zurück
			yield return (startOffset, element);

			//Wenn das Element über das Ende hinaus geht, beende die Suche
			if (currentOffset >= range.End)
				break;
		}
	}
}

public sealed record SheetDisplayEmptyLine : SheetDisplayLine
{
	public override IEnumerable<SheetDisplayLineElement> GetElements()
        => Enumerable.Empty<SheetDisplayLineElement>();
}

//public sealed record SheetDisplaySpacerLine(int Length) : SheetDisplayLine
//{
//    public override IEnumerable<SheetDisplayLineElement> GetElements()
//    {
//        yield return new SheetDisplayLineSpace(Length);
//    }
//}

public sealed record SheetDisplayTextLine : SheetDisplayLine
{
    private readonly SheetDisplayLineElement[] elements;

    public SheetDisplayTextLine(params SheetDisplayLineElement[] elements) : this((IEnumerable<SheetDisplayLineElement>)elements) { }
    public SheetDisplayTextLine(IEnumerable<SheetDisplayLineElement> elements)
    {
        this.elements = elements.ToArray();
    }

    public override IEnumerable<SheetDisplayLineElement> GetElements() => elements;

    public class Builder : SheetDisplayTextLineBuilder<SheetDisplayTextLine>
    {
        public override SheetDisplayTextLine CreateDisplayLine(ISheetDisplayLineEditing editing) => new(Elements)
		{
			Editing = editing
		};

        public override int CompareTo(SheetDisplayLineBuilder? other)
        {
            if (other is Builder)
                return 0;

            return 1;
        }
    }
}

public sealed record SheetDisplayChordLine : SheetDisplayLine
{
    private readonly SheetDisplayLineElement[] elements;

    public SheetDisplayChordLine(params SheetDisplayLineElement[] elements) : this((IEnumerable<SheetDisplayLineElement>)elements) { }
    public SheetDisplayChordLine(IEnumerable<SheetDisplayLineElement> elements)
    {
        this.elements = elements.ToArray();
    }

    public override IEnumerable<SheetDisplayLineElement> GetElements() => elements;

    public class Builder : SheetDisplayTextLineBuilder<SheetDisplayChordLine>
    {
        public override SheetDisplayChordLine CreateDisplayLine(ISheetDisplayLineEditing editing) => new(Elements)
		{
			Editing = editing
		};

        public override int CompareTo(SheetDisplayLineBuilder? other)
        {
            if (other is SheetDisplayTextLine.Builder)
                return -1;

            if (other is Builder)
                return 0;

            return 1;
        }
    }
}