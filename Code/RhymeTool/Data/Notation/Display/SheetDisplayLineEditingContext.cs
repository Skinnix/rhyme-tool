namespace Skinnix.RhymeTool.Data.Notation.Display;

public record struct SheetDisplayLineEditingContext(SimpleRange SelectionRange)
{
	public Func<SheetLine?>? GetLineBefore { get; init; }
	public Func<SheetLine?>? GetLineAfter { get; init; }
}
