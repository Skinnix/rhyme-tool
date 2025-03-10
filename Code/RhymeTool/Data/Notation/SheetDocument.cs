﻿using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Features;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetDocument
{
	public event EventHandler<ModifiedEventArgs>? LinesModified;
	public event EventHandler? TitlesChanged;
	public event EventHandler<ModifiedEventArgs>? FeaturesModified;

	public string? Label { get; set; }

	public FeatureCollection GlobalFeatures { get; }
	public SheetLineCollection Lines { get; }

    public SheetDocument()
	{
		GlobalFeatures = new(this);
		Lines = new(this);

		Lines.Modified += (s, e) => LinesModified?.Invoke(this, e);
		Lines.TitlesChanged += (s, e) => TitlesChanged?.Invoke(this, e);
		GlobalFeatures.Modified += (s, e) => FeaturesModified?.Invoke(this, e);
	}

	public SheetDocument(params SheetLine[] lines) : this((IEnumerable<SheetLine>)lines) { }
    public SheetDocument(IEnumerable<SheetLine> lines)
    {
		GlobalFeatures = new(this);
		Lines = new(this, lines);

		Lines.Modified += (s, e) => LinesModified?.Invoke(this, e);
		Lines.TitlesChanged += (s, e) => TitlesChanged?.Invoke(this, e);
		GlobalFeatures.Modified += (s, e) => FeaturesModified?.Invoke(this, e);
	}

	private SheetDocument(Func<SheetDocument, SheetLineCollection> getLines, Func<SheetDocument, FeatureCollection> getFeatures)
	{
		GlobalFeatures = getFeatures(this);
		Lines = getLines(this);
		Lines.Modified += (s, e) => LinesModified?.Invoke(this, e);
		Lines.TitlesChanged += (s, e) => TitlesChanged?.Invoke(this, e);
		GlobalFeatures.Modified += (s, e) => FeaturesModified?.Invoke(this, e);
	}

	public IEnumerable<SheetSegment> FindSegments()
		=> Lines.OfType<ISheetTitleLine>()
		.Select(t => (Line: t, IsTitle: t.IsTitleLine(out var title), Title: title))
		.Where(t => t.IsTitle)
		.Select(t => new SheetSegment(t.Title, t.Line));

	public Stored Store() => new(this);

	public readonly record struct Stored : IStored<SheetDocument>
	{
		private readonly FeatureCollection.Stored globalFeatures;
		private readonly SheetLineCollection.Stored lines;

		public Stored(SheetDocument document)
			: this(document.GlobalFeatures.Store(), document.Lines.Store())
		{ }

		private Stored(FeatureCollection.Stored globalFeatures, SheetLineCollection.Stored lines)
		{
			this.globalFeatures = globalFeatures;
			this.lines = lines;
		}

		public SheetDocument Restore()
			=> new SheetDocument(lines.Restore, globalFeatures.Restore);

		internal void Apply(SheetDocument document)
		{
			globalFeatures.Apply(document.GlobalFeatures);
			lines.Apply(document.Lines);
		}

		/*public Stored OptimizeWith(Stored other)
			=> new(globalFeatures.OptimizeWith(other.globalFeatures), lines.OptimizeWith(other.lines));*/
	}
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
