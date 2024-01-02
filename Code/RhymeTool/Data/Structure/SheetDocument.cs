namespace Skinnix.RhymeTool.Data.Structure;

public class SheetDocument
{
	public string Label { get; set; } = string.Empty;

	public List<SheetSegment> Segments { get; } = new();

	public SheetDocument() { }

	public SheetDocument(params SheetSegment[] segments) : this((IEnumerable<SheetSegment>)segments) { }
	public SheetDocument(IEnumerable<SheetSegment> segments)
	{
		Segments.AddRange(segments);
	}
}

public class SheetSegment
{
	public List<SheetLine> Lines { get; } = new();

	public string? Title { get; set; }

	public SheetSegment() { }

	public SheetSegment(params SheetLine[] lines) : this((IEnumerable<SheetLine>)lines) { }
	public SheetSegment(IEnumerable<SheetLine> lines)
	{
		Lines.AddRange(lines);
	}
}
