﻿namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineEditing
{
	public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter);
	public LineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false);
}

public record LineEditResult(bool Success, SimpleRange? NewSelection)
{
	public static LineEditResult Fail => new(false, null);

	public List<SheetDisplayLineElement> ModifiedElements { get; init; } = new();
}

public record MetalineEditResult(int Line, LineEditResult LineResult)
{
	public bool Success => LineResult.Success;
}