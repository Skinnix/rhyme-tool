namespace Skinnix.RhymeTool.Data.Notation;

public class SheetDocument
{
    public string? Label { get; set; }

	//public List<SheetSegment> Segments { get; } = new();
	public List<SheetLine> Lines { get; } = new();

    public SheetDocument() { }

    public SheetDocument(params SheetLine[] lines) : this((IEnumerable<SheetLine>)lines) { }
    public SheetDocument(IEnumerable<SheetLine> lines)
    {
        Lines.AddRange(lines);
    }

	public IEnumerable<SheetSegment> FindSegments()
		=> Lines.OfType<ISheetTitleLine>()
		.Select(t => (Line: t, IsTitle: t.IsTitleLine(out var title), Title: title))
		.Where(t => t.IsTitle)
		.Select(t => new SheetSegment(t.Title, t.Line));
}

public class SheetSegment
{
	public string? Title { get; }
	public ISheetTitleLine TitleLine { get; }

	public SheetSegment(string? title, ISheetTitleLine titleLine)
	{
		Title = title;
		TitleLine = titleLine;
	}
}

//public class SheetSegment
//{
//    public List<SheetLine> Lines { get; } = new();

//	public SheetSegmentTitleLine? TitleLine { get; }

//    public SheetSegment() { }

//    public SheetSegment(params SheetLine[] lines) : this((IEnumerable<SheetLine>)lines) { }
//    public SheetSegment(IEnumerable<SheetLine> lines)
//    {
//        Lines.AddRange(lines);
//    }
//}
