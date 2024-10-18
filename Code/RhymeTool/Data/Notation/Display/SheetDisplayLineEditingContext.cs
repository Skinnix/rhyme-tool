namespace Skinnix.RhymeTool.Data.Notation.Display;

public record struct SheetDisplayLineEditingContext(SheetLineContext LineContext, SimpleRange SelectionRange, bool JustSelected)
{
	public Func<SheetLine?>? GetLineBefore { get; init; }
	public Func<SheetLine?>? GetLineAfter { get; init; }
}
