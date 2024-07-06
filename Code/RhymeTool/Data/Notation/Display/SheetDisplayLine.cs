using System.Xml.Linq;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public abstract record SheetDisplayLine(int Id)
{
	public required ISheetDisplayLineEditing Editing { get; init; }

    public abstract IEnumerable<SheetDisplayLineElement> GetElements();

	public IEnumerable<(int Offset, int Length, SheetDisplayLineElement Element)> GetElementsIn(SimpleRange? range, ISheetFormatter? formatter)
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
			if (currentOffset <= range?.Start)
				continue;

			//Gib das Element zurück
			yield return (startOffset, length, element);

			//Wenn das Element über das Ende hinaus geht, beende die Suche
			if (currentOffset >= range?.End)
				break;
		}
	}
}

public sealed record SheetDisplayEmptyLine(int Id) : SheetDisplayLine(Id)
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

public sealed record SheetDisplaySegmentTitleLine : SheetDisplayLine
{
	public string Title { get; init; }

	public SheetDisplaySegmentTitleLine(int id, string title)
		: base(id)
	{
		Title = title;
	}

	public override IEnumerable<SheetDisplayLineElement> GetElements()
		=> [
			new SheetDisplayLineSegmentTitleBracket("["),
			new SheetDisplayLineSegmentTitleText(Title),
			new SheetDisplayLineSegmentTitleBracket("]"),
		];
}

public sealed record SheetDisplayTextLine : SheetDisplayLine
{
    private readonly SheetDisplayLineElement[] elements;

    public SheetDisplayTextLine(int id, params SheetDisplayLineElement[] elements) : this(id, (IEnumerable<SheetDisplayLineElement>)elements) { }
    public SheetDisplayTextLine(int id, IEnumerable<SheetDisplayLineElement> elements)
		: base(id)
    {
        this.elements = elements.ToArray();
    }

    public override IEnumerable<SheetDisplayLineElement> GetElements() => elements;

    public class Builder : SheetDisplayTextLineBuilder<SheetDisplayTextLine>
    {
        public override SheetDisplayTextLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing) => new(id, Elements)
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

    public SheetDisplayChordLine(int id, params SheetDisplayLineElement[] elements) : this(id, (IEnumerable<SheetDisplayLineElement>)elements) { }
    public SheetDisplayChordLine(int id, IEnumerable<SheetDisplayLineElement> elements)
		: base(id)
    {
        this.elements = elements.ToArray();
    }

    public override IEnumerable<SheetDisplayLineElement> GetElements() => elements;

    public class Builder : SheetDisplayTextLineBuilder<SheetDisplayChordLine>
    {
        public override SheetDisplayChordLine CreateDisplayLine(int id, ISheetDisplayLineEditing editing) => new(id, Elements)
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