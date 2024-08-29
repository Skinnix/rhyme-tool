using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineEditing
{
	SheetLine Line { get; }
	int LineId { get; }

	DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, string content, bool isMultilineEdit, ISheetEditorFormatter? formatter = null);
	DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, DeleteDirection direction, DeleteType type, bool isMultilineEdit, ISheetEditorFormatter? formatter = null);

	MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, bool isMultilineEdit, ISheetEditorFormatter? formatter = null)
	{
		var tryResult = TryInsertContent(context, content, isMultilineEdit, formatter);
		if (!tryResult.Success)
			return MetalineEditResult.Fail(tryResult.FailReason);

		return tryResult.Execute();
	}

	MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, DeleteDirection direction, DeleteType type, bool isMultilineEdit, ISheetEditorFormatter? formatter = null)
	{
		var tryResult = TryDeleteContent(context, direction, type, isMultilineEdit, formatter);
		if (!tryResult.Success)
			return MetalineEditResult.Fail(tryResult.FailReason);

		return tryResult.Execute();
	}

	public ReasonBase? SupportsEdit(SheetDisplayMultiLineEditingContext context);
}

public enum DeleteDirection
{
	Backward,
	Forward
}

public enum DeleteType
{
	Character,
	Word
}

public record DelayedMetalineEditResult
{
	public Func<MetalineEditResult>? Execute { get; init; }
	public ReasonBase? FailReason { get; init; }

	[MemberNotNullWhen(true, nameof(Execute)), MemberNotNullWhen(false, nameof(FailReason))]
	public bool Success => Execute is not null;

	public DelayedMetalineEditResult(Func<MetalineEditResult> execute)
	{
		Execute = execute;
	}

	private DelayedMetalineEditResult(ReasonBase failReason)
	{
		FailReason = failReason;
	}

	public static DelayedMetalineEditResult Fail(ReasonBase reason)
		=> new(reason);
}

public record MetalineEditResult
{
	public MetalineSelectionRange? NewSelection { get; init; }
	public ReasonBase? FailReason { get; init; }

	public bool RemoveLine { get; init; }
	public bool RemoveLineAfter { get; init; }
	public bool RemoveLineBefore { get; init; }
	public IReadOnlyList<SheetLine> InsertLinesBefore { get; init; } = [];
	public IReadOnlyList<SheetLine> InsertLinesAfter { get; init; } = [];
	public IReadOnlyList<SheetDisplayLineElement> ModifiedElements { get; init; } = [];

	[MemberNotNullWhen(true, nameof(NewSelection)), MemberNotNullWhen(false, nameof(FailReason))]
	public bool Success => NewSelection is not null;

	public MetalineEditResult(MetalineSelectionRange newSelection)
	{
		NewSelection = newSelection;
	}

	private MetalineEditResult(ReasonBase failReason)
	{
		FailReason = failReason;
	}

	public static MetalineEditResult Fail(ReasonBase reason)
		=> new(reason);

	public void Execute(SheetDocument document, SheetLine line)
	{
		//Füge ggf. Zeilen hinzu oder entferne sie
		document.Lines.InsertAndRemove(line, RemoveLine, RemoveLineBefore, RemoveLineAfter,
			InsertLinesBefore, InsertLinesAfter);
	}
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
		=> new MetalineEditResult(new MetalineSelectionRange(editing, newSelection));
}
