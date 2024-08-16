namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineEditing
{
	public SheetLine Line { get; }
	public int LineId { get; }

	public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetEditorFormatter? formatter = null);
	public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetEditorFormatter? formatter = null);
}

public record struct SheetDisplayLineEditingContext(SimpleRange SelectionRange)
{
	public Func<SheetLine?>? GetLineBefore { get; init; }
	public Func<SheetLine?>? GetLineAfter { get; init; }
}

public record MetalineEditResult(bool Success, MetalineSelectionRange? NewSelection)
{
	public ReasonBase? FailReason { get; init; }

	public bool RemoveLine { get; init; }
	public bool RemoveLineAfter { get; init; }
	public bool RemoveLineBefore { get; init; }
	public IReadOnlyList<SheetLine> InsertLinesBefore { get; init; } = [];
	public IReadOnlyList<SheetLine> InsertLinesAfter { get; init; } = [];
	public IReadOnlyList<SheetDisplayLineElement> ModifiedElements { get; init; } = [];

	public static MetalineEditResult Fail(ReasonBase reason)
		=> new(false, null) { FailReason = reason };
}

public record MetalineSelectionRange
{
	public SheetLine Metaline { get; init; }
	public int? LineId { get; init; }
	public int? LineIndex { get; init; }
	public SimpleRange Range { get; init; }

	public MetalineSelectionRange(ISheetDisplayLineEditing editing, SimpleRange range)
	{
		Metaline = editing.Line;
		LineId = editing.LineId;
		Range = range;
	}

	public MetalineSelectionRange(SheetLine metaline, SimpleRange range, int lineIndex)
	{
		Metaline = metaline;
		Range = range;
		LineIndex = lineIndex;
	}
}

public static class SheetDisplayLineEditingExtensions
{
	public static MetalineEditResult CreateSuccessEditResult(this ISheetDisplayLineEditing editing, SimpleRange newSelection)
		=> new MetalineEditResult(true, new MetalineSelectionRange(editing, newSelection));
}
