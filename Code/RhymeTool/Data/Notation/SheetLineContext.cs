using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation.Display;
using Skinnix.RhymeTool.Data.Notation.Features;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetLineContext
{
	private IDocumentFeature[] features;

	public SheetDocument Document { get; }
	public SheetLine Line { get; }

	public SheetLineContext? Previous { get; }

	public SheetLineContext(SheetDocument document, SheetLine line, SheetLineContext? previous, IDocumentFeature[] features)
	{
		Document = document;
		Line = line;
		Previous = previous;
		this.features = features;
	}

	public IEnumerable<IDocumentFeature> GetFeatures()
		=> features;

	public IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
		=> Line.CreateDisplayLines(this, formatter);
}
