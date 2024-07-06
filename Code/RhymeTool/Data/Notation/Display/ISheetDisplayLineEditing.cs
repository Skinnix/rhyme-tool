namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineEditing
{
	public SheetLine Line { get; }
	public int LineId { get; }

	public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetFormatter? formatter = null);
	public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetFormatter? formatter = null);
}

public record struct SheetDisplayLineEditingContext(SliceSelection Selection)
{
	public Func<SheetLine?>? GetLineBefore { get; init; }
	public Func<SheetLine?>? GetLineAfter { get; init; }
}

public readonly record struct SliceSelection(SliceSelectionAnchor Start, SliceSelectionAnchor End)
{
}

public readonly record struct SliceSelectionAnchor(int ComponentIndex, int BlockIndex, int SliceIndex, int ContentOffset, int EditOffset)
{
	public bool Virtual { get; init; }

	public int VirtualContentOffset => ContentOffset + EditOffset;
	public int NonVirtualContentOffset => Virtual ? ContentOffset : ContentOffset + EditOffset;
}

public record MetalineEditResult(bool Success, MetalineSliceSelection? NewSelection)
{
	public static MetalineEditResult Fail => new(false, null);

	public bool RemoveLine { get; init; }
	public bool RemoveLineAfter { get; init; }
	public bool RemoveLineBefore { get; init; }
	public IReadOnlyList<SheetLine> InsertLinesBefore { get; init; } = [];
	public IReadOnlyList<SheetLine> InsertLinesAfter { get; init; } = [];
	public IReadOnlyList<SheetDisplayLineElement> ModifiedElements { get; init; } = [];
}

public record MetalineSliceSelection(MetalineSliceAnchor Start, MetalineSliceAnchor End)
{
	public static MetalineSliceSelection CursorAt(MetalineSliceAnchor position) => new(position, position);
}

public record MetalineSliceAnchor
{
	public SheetLine Metaline { get; init; }
	public int? LineId { get; init; }
	public int? LineIndex { get; init; }
	public MetalineSliceIndex? Slice { get; init; }
	public int? EditOffset { get; init; } //null = Ende

	public MetalineSliceAnchor(ISheetDisplayLineEditing editing, MetalineSliceIndex? slice, int? editOffset)
	{
		Metaline = editing.Line;
		LineId = editing.LineId;
		Slice = slice;
		EditOffset = editOffset;
	}

	public MetalineSliceAnchor(SheetLine metaline, int lineIndex, MetalineSliceIndex? slice, int? editOffset)
	{
		Metaline = metaline;
		LineIndex = lineIndex;
		Slice = slice;
		EditOffset = editOffset;
	}

	public static MetalineSliceAnchor LineOffset(ISheetDisplayLineEditing editing, int? offset) => new(editing, null, offset);

	public static MetalineSliceAnchor StartOfLine(ISheetDisplayLineEditing editing) => new(editing, null, 0);
	public static MetalineSliceAnchor EndOfLine(ISheetDisplayLineEditing editing) => new(editing, null, null);
}

public record MetalineSliceIndex(int ComponentIndex, int? BlockIndex, int? SliceIndex)
{
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

}
