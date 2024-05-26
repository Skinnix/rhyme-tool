using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.Data.Notation.Display.Caching;

public class SheetLineCache : DeepObservableBase
{
	public SheetLine Line { get; }

	private ImmutableList<SheetDisplayLine>? displayLines;
	private ISheetFormatter? formatter;

	public SheetLineCache(SheetLine line)
	{
		Line = Register(line);
	}

	protected override void RaiseModified(ModifiedEventArgs args)
	{
		base.RaiseModified(args);

		displayLines = null;
	}

	public IReadOnlyList<SheetDisplayLine> GetDisplayLines(ISheetBuilderFormatter? formatter = null)
	{
		if (this.formatter != formatter)
			displayLines = null;

		if (displayLines == null)
		{
			displayLines = ImmutableList.CreateRange(Line.CreateDisplayLines(formatter));
			this.formatter = formatter;
		}

		return displayLines.AsReadOnly();
	}
}
