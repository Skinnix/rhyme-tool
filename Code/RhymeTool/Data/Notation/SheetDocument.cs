using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Features;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetDocument
{
	public event EventHandler<ModifiedEventArgs>? LinesModified;
	public event EventHandler? TitlesChanged;
	public event EventHandler<ModifiedEventArgs>? FeaturesModified;

	public string? Label { get; set; }

	public FeatureCollection GlobalFeatures { get; } = new();
	public SheetLineCollection Lines { get; }

    public SheetDocument()
	{
		Lines = new(this);
		Lines.Modified += (s, e) => LinesModified?.Invoke(this, e);
		Lines.TitlesChanged += (s, e) => TitlesChanged?.Invoke(this, e);
		GlobalFeatures.Modified += (s, e) => FeaturesModified?.Invoke(this, e);
	}

	public SheetDocument(params SheetLine[] lines) : this((IEnumerable<SheetLine>)lines) { }
    public SheetDocument(IEnumerable<SheetLine> lines)
    {
		Lines = new(this, lines);
		Lines.Modified += (s, e) => LinesModified?.Invoke(this, e);
		Lines.TitlesChanged += (s, e) => TitlesChanged?.Invoke(this, e);
		GlobalFeatures.Modified += (s, e) => FeaturesModified?.Invoke(this, e);
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
