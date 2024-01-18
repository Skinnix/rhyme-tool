using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public abstract class SheetLineComponent : DeepObservableBase
{
	public bool IsSelected { get; set; }

	public abstract SheetLineComponentInsertResult InsertContent(int insertOffset, string content, ISheetFormatter? formatter);
	public abstract SheetLineComponentCutResult CutContent(int cutOffset, int cutLength, ISheetFormatter? formatter);
}

public record SheetLineComponentCutResult
{
	public bool Remove { get; init; }
	public SheetLineComponent? Replacement { get; init; }
}

public record SheetLineComponentInsertResult
{
	
}