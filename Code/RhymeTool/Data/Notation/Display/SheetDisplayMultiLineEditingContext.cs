﻿using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public record SheetDisplayMultiLineEditingContext(SheetDocument Document,
	ISheetDisplayLineEditing StartLine, int SelectionStart,
	ISheetDisplayLineEditing EndLine, int SelectionEnd,
	bool JustSelected, bool AfterComposition)
{
	public IReadOnlyList<SheetLine> LinesBetween { get; init; } = [];

	public MultilineEditResult DeleteContent(DeleteDirection direction, ISheetEditorFormatter? formatter = null)
	{
		var contexts = Document.Lines.GetLinesWithContext().ToArray();

		//Trenne das Ende der ersten Zeile ab
		var firstLineLineContext = contexts.First(c => c.Line == StartLine.Line);
		var firstLineContext = new SheetDisplayLineEditingContext(firstLineLineContext, SimpleRange.CursorAtEnd, SimpleRange.AllFromStart(SelectionStart), JustSelected, AfterComposition)
		{
			GetLineAfter = () => LinesBetween.Count == 0 ? EndLine.Line : LinesBetween[1],
		};
		var firstLineResult = StartLine.TryDeleteContent(firstLineContext, this, DeleteDirection.Forward, DeleteType.Character, formatter);
		if (!firstLineResult.Success)
			return MultilineEditResult.Fail(firstLineResult.FailReason);

		//Trenne den Anfang der letzten Zeile ab
		var lastLineLineContext = contexts.First(c => c.Line == EndLine.Line);
		var lastLineContext = new SheetDisplayLineEditingContext(lastLineLineContext, SimpleRange.CursorAtStart, SimpleRange.AllToEnd(SelectionEnd), JustSelected, AfterComposition)
		{
			GetLineBefore = () => LinesBetween.Count == 0 ? StartLine.Line : LinesBetween[^1],
		};
		var lastLineResult = EndLine.TryDeleteContent(lastLineContext, this, DeleteDirection.Backward, DeleteType.Character, formatter);
		if (!lastLineResult.Success)
			return MultilineEditResult.Fail(lastLineResult.FailReason);

		//Führe die Bearbeitungen aus
		var lineEditResults = new MetalineEditResult[LinesBetween.Count + 2];
		var firstLineExecuteResult = firstLineResult.Execute();
		firstLineExecuteResult.Execute(Document, StartLine.Line);
		var lastLineExecuteResult = lastLineResult.Execute();
		lastLineExecuteResult.Execute(Document, EndLine.Line);

		//Lösche die Zeilen dazwischen
		foreach (var lineBetween in LinesBetween)
			Document.Lines.Remove(lineBetween);

		//Ist die letzte Zeile noch vorhanden?
		MetalineEditResult? combineResult = null;
		if (!lastLineExecuteResult.RemoveLine)
		{
			//Kombiniere die erste und letzte Zeile, indem am Anfang der letzten Zeile rückwärts gelöscht wird
			combineResult = EndLine.DeleteContent(new SheetDisplayLineEditingContext(lastLineLineContext, SimpleRange.CursorAtStart, SimpleRange.CursorAtStart, false, AfterComposition)
			{
				GetLineBefore = () => StartLine.Line,
			}, this, DeleteDirection.Backward, DeleteType.Character, formatter);
			combineResult.Execute(Document, EndLine.Line);
		}

		//Verwende die Selektion der Kombination der Zeilen. Sollte das nicht funktioniert haben, verwende die Selektion der ersten oder letzten Zeile
		var selection = combineResult?.NewSelection
			?? firstLineExecuteResult.NewSelection ?? lastLineExecuteResult.NewSelection
			?? new MetalineSelectionRange(StartLine, SimpleRange.CursorAt(SelectionStart));

		//Erzeuge das Ergebnis
		return new MultilineEditResult(selection)
		{
			FirstLineResult = firstLineExecuteResult,
			LastLineResult = lastLineExecuteResult,
			CombineLinesResult = combineResult
		};
	}

	public MultilineEditResult InsertContent(string content, ISheetEditorFormatter? formatter = null)
	{
		var contexts = Document.Lines.GetLinesWithContext().ToArray();

		//Füge den neuen Content in die erste Zeile ein
		var firstLineLineContext = contexts.First(c => c.Line == StartLine.Line);
		var firstLineContext = new SheetDisplayLineEditingContext(firstLineLineContext, SimpleRange.CursorAtStart, SimpleRange.AllFromStart(SelectionStart), JustSelected, AfterComposition)
		{
			GetLineAfter = () => LinesBetween.Count == 0 ? EndLine.Line : LinesBetween[1],
		};
		var firstLineResult = StartLine.TryInsertContent(firstLineContext, this, content, formatter);
		if (!firstLineResult.Success)
			return MultilineEditResult.Fail(firstLineResult.FailReason);

		//Trenne den Anfang der letzten Zeile ab
		var lastLineLineContext = contexts.First(c => c.Line == EndLine.Line);
		var lastLineContext = new SheetDisplayLineEditingContext(lastLineLineContext, SimpleRange.CursorAtEnd, SimpleRange.AllToEnd(SelectionEnd), JustSelected, AfterComposition)
		{
			GetLineBefore = () => LinesBetween.Count == 0 ? StartLine.Line : LinesBetween[^1],
		};
		var lastLineResult = EndLine.TryDeleteContent(lastLineContext, this, DeleteDirection.Backward, DeleteType.Character, formatter);
		if (!lastLineResult.Success)
			return MultilineEditResult.Fail(lastLineResult.FailReason);

		//Führe die Bearbeitungen aus
		var lineEditResults = new MetalineEditResult[LinesBetween.Count + 2];
		var firstLineExecuteResult = firstLineResult.Execute();
		firstLineExecuteResult.Execute(Document, StartLine.Line);
		var lastLineExecuteResult = lastLineResult.Execute();
		lastLineExecuteResult.Execute(Document, EndLine.Line);

		//Lösche die Zeilen dazwischen
		foreach (var lineBetween in LinesBetween)
			Document.Lines.Remove(lineBetween);

		//Kombiniere die erste und letzte Zeile, indem am Anfang der letzten Zeile rückwärts gelöscht wird
		var combineResult = EndLine.DeleteContent(new SheetDisplayLineEditingContext(lastLineLineContext, SimpleRange.CursorAtStart, SimpleRange.CursorAtStart, false, AfterComposition)
		{
			GetLineBefore = () => StartLine.Line,
		}, this, DeleteDirection.Backward, DeleteType.Character, formatter);
		combineResult.Execute(Document, EndLine.Line);

		//Verwende die Selektion der Kombination der Zeilen. Sollte das nicht funktioniert haben, verwende die Selektion der ersten oder letzten Zeile
		var selection = combineResult.NewSelection
			?? firstLineExecuteResult.NewSelection ?? lastLineExecuteResult.NewSelection
			?? new MetalineSelectionRange(StartLine, SimpleRange.CursorAt(SelectionStart));

		//Erzeuge das Ergebnis
		return new MultilineEditResult(selection)
		{
			FirstLineResult = firstLineExecuteResult,
			LastLineResult = lastLineExecuteResult,
			CombineLinesResult = combineResult
		};
	}
}

public record MultilineEditResult
{
	public MetalineSelectionRange? NewSelection { get; init; }
	public ReasonBase? FailReason { get; init; }

	public MetalineEditResult? FirstLineResult { get; init; }
	public MetalineEditResult? LastLineResult { get; init; }
	public MetalineEditResult? CombineLinesResult { get; init; }

	[MemberNotNullWhen(true, nameof(NewSelection)), MemberNotNullWhen(false, nameof(FailReason))]
	public bool Success => NewSelection is not null;

	public MultilineEditResult(MetalineSelectionRange newSelection)
	{
		NewSelection = newSelection;
	}

	private MultilineEditResult(ReasonBase failReason)
	{
		FailReason = failReason;
	}

	public static MultilineEditResult Fail(ReasonBase reason)
		=> new(reason);
}
