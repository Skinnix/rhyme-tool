namespace Skinnix.RhymeTool.Data.Notation.Display;

public record struct SheetDisplayLineEditingContext(SheetLineContext LineContext, SimpleRange SelectionRange, SimpleRange? EditRange, bool JustSelected, bool AfterCompose)
{
	public Func<SheetLine?>? GetLineBefore { get; init; }
	public Func<SheetLine?>? GetLineAfter { get; init; }

	public SimpleRange EffectiveRange => EditRange ?? SelectionRange;
}
