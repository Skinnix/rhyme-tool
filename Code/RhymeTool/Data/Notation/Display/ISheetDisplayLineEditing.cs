﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineEditing
{
	SheetLine Line { get; }
	int LineId { get; }

	DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext, string content, ISheetEditorFormatter? formatter = null);
	DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext, DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter = null);

	MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext, string content, ISheetEditorFormatter? formatter = null)
	{
		var tryResult = TryInsertContent(context, multilineContext, content, formatter);
		if (!tryResult.Success)
			return MetalineEditResult.Fail(tryResult.FailReason);

		return tryResult.Execute();
	}

	MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext, DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter = null)
	{
		var tryResult = TryDeleteContent(context, multilineContext, direction, type, formatter);
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

	public DelayedMetalineEditResult TransformSuccess(Func<MetalineEditResult, MetalineEditResult> transform)
	{
		if (!Success)
			return this;

		return new DelayedMetalineEditResult(() =>
		{
			var result = Execute!();
			if (!result.Success)
				return result;

			return transform(result);
		});
	}
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

	public static MetalineEditResult SuccessWithoutSelection()
		=> new((MetalineSelectionRange?)null!);

	public void Execute(SheetDocument document, SheetLine line)
	{
		//Füge ggf. Zeilen hinzu oder entferne sie
		document.Lines.InsertAndRemove(line, RemoveLine, RemoveLineBefore, RemoveLineAfter,
			InsertLinesBefore, InsertLinesAfter);
	}
}

public record MetalineSelectionRange
{
	public const int FIRST_LINE = -1;
	public const int LAST_LINE = -2;

	public SheetLine StartMetaline { get; init; }
	public SheetLine EndMetaline { get; init; }

	public int StartLineId { get; init; }
	public int EndLineId { get; init; }

	public SimpleRange Range { get; init; }

	public MetalineSelectionRange(ISheetDisplayLineEditing editing, SimpleRange range)
	{
		StartMetaline = EndMetaline = editing.Line;
		StartLineId = EndLineId = editing.LineId;
		Range = range;
	}

	public MetalineSelectionRange(SheetLine metaline, SimpleRange range, int lineId)
	{
		StartMetaline = EndMetaline = metaline;
		Range = range;
		StartLineId = EndLineId = lineId;
	}
}

public static class SheetDisplayLineEditingExtensions
{
	public static MetalineEditResult CreateSuccessEditResult(this ISheetDisplayLineEditing editing, SimpleRange newSelection)
		=> new MetalineEditResult(new MetalineSelectionRange(editing, newSelection));
}
