using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Data.Notation.Display;
using Skinnix.RhymeTool.Data;
using System.Runtime.Serialization;

namespace Skinnix.RhymeTool.Client.Components.Editing;

partial class SheetEditor
{
	[JSInvokable]
	public JsMetalineEditResult OnBeforeInput(InputEventData data)
	{
		if (Document is null) throw new InvalidOperationException("Editor nicht initialisiert");

		//Sind Start- und Endzeile unterschiedlich?
		if (data.Selection.Start.Metaline != data.Selection.End.Metaline
			|| data.Selection.Start.Line != data.Selection.End.Line)
		{
			//Mehrere Zeilen bearbeiten
			var editResult = EditMultiLine(data);

			if (!editResult.Success)
				return new JsMetalineEditResult(false, null, editResult.FailReason);

			//Erzeuge Ergebnis
			var selection = editResult.NewSelection is null ? null
				: new JsMetalineSelectionRange(editResult.NewSelection.Metaline.Guid, editResult.NewSelection.LineId,
					editResult.NewSelection.LineIndex, editResult.NewSelection.Range);
			return new JsMetalineEditResult(true, selection, null);
		}
		else
		{
			//Einzelne Zeile bearbeiten
			var editResult = EditLine(data);

			if (!editResult.Success)
				return new JsMetalineEditResult(false, null, editResult.FailReason);

			//Erzeuge Ergebnis
			var selection = editResult.NewSelection is null ? null
				: new JsMetalineSelectionRange(editResult.NewSelection.Metaline.Guid, editResult.NewSelection.LineId,
					editResult.NewSelection.LineIndex, editResult.NewSelection.Range);
			return new JsMetalineEditResult(true, selection, null);
		}
	}

	protected MetalineEditResult EditLine(InputEventData data)
	{
		if (Document is null) throw new InvalidOperationException("Editor nicht initialisiert");

		//Finde den Editor
		var editor = lineEditors[data.Selection.Start.Metaline];

		//Finde die Displayzeile
		var line = editor.TryGetLine(data.Selection.Start.Line);
		if (line is null)
			return MetalineEditResult.Fail(LineNotFound);

		//Auswahl
		var selectionRange = new SimpleRange(data.Selection.Start.Offset, data.Selection.End.Offset);
		var context = new SheetDisplayLineEditingContext(selectionRange)
		{
			GetLineBefore = () => Document.Lines.GetLineBefore(line.Editing.Line),
			GetLineAfter = () => Document.Lines.GetLineAfter(line.Editing.Line),
		};

		//Bearbeitungstyp
		var editResult = GetEditType(data) switch
		{
			EditType.InsertContent => line.Editing.InsertContent(context, data.Data, false, Formatter),
			EditType.InsertLine => line.Editing.InsertContent(context, "\n", false, Formatter),
			EditType.DeleteBackward => line.Editing.DeleteContent(context, DeleteDirection.Backward, DeleteType.Character, false, Formatter),
			EditType.DeleteForward => line.Editing.DeleteContent(context, DeleteDirection.Forward, DeleteType.Character, false, Formatter),
			EditType.DeleteWordBackward => line.Editing.DeleteContent(context, DeleteDirection.Backward, DeleteType.Word, false, Formatter),
			EditType.DeleteWordForward => line.Editing.DeleteContent(context, DeleteDirection.Forward, DeleteType.Word, false, Formatter),
			_ => MetalineEditResult.Fail(UnknownEditType)
		};

		//Nicht erfolgreich?
		if (!editResult.Success)
			return editResult;

		//Führe die Änderungen durch
		editResult.Execute(Document, line.Editing.Line);
		return editResult;
	}

	protected MultilineEditResult EditMultiLine(InputEventData data)
	{
		if (Document is null) throw new InvalidOperationException("Editor nicht initialisiert");

		//Finde den Editor
		var startEditor = lineEditors[data.Selection.Start.Metaline];
		var endEditor = lineEditors[data.Selection.End.Metaline];

		//Finde die Displayzeilen
		var startLine = startEditor.TryGetLine(data.Selection.Start.Line);
		var endLine = endEditor.TryGetLine(data.Selection.End.Line);
		if (startLine is null || endLine is null)
			return MultilineEditResult.Fail(LineNotFound);

		//Erzeuge Kontext
		var context = new SheetDisplayMultiLineEditingContext(Document, startLine.Editing, data.Selection.Start.Offset,
			endLine.Editing, data.Selection.End.Offset);

		//Sind Start- und Endzeile mit dem Kontext kompatibel?
		var supportsEdit = context.StartLine.SupportsEdit(context);
		if (supportsEdit is not null)
			return MultilineEditResult.Fail(supportsEdit);
		supportsEdit = context.EndLine.SupportsEdit(context);
		if (supportsEdit is not null)
			return MultilineEditResult.Fail(supportsEdit);

		//Bearbeitungstyp
		var editResult = GetEditType(data) switch
		{
			EditType.InsertContent => context.InsertContent(data.Data, Formatter),
			EditType.InsertLine => context.InsertContent("\n", Formatter),
			EditType.DeleteBackward or EditType.DeleteWordBackward => context.DeleteContent(DeleteDirection.Backward, Formatter),
			EditType.DeleteForward or EditType.DeleteWordForward => context.DeleteContent(DeleteDirection.Forward, Formatter),
			_ => MultilineEditResult.Fail(UnknownEditType)
		};

		return editResult;
	}

	private static EditType? GetEditType(InputEventData data) => data.InputType switch
	{
		"insertFromDrop" or "insertFromPaste" or "insertFromPasteAsQuotation" or "insertLink" or "insertText" => (EditType?)EditType.InsertContent,
		"insertLineBreak" or "insertParagraph" => (EditType?)EditType.InsertLine,
		"deleteByCut" or "deleteByDrag" or "deleteContentBackward" or "deleteContent" => (EditType?)EditType.DeleteBackward,
		"deleteContentForward" => (EditType?)EditType.DeleteForward,
		"deleteWord" or "deleteWordBackward" => (EditType?)EditType.DeleteWordBackward,
		"deleteWordForward" => (EditType?)EditType.DeleteWordForward,
		_ => null,
	};

	private enum EditType
	{
		InsertContent,
		InsertLine,
		DeleteBackward,
		DeleteForward,
		DeleteWordForward,
		DeleteWordBackward,
	}

	public record JsMetalineEditResult(bool Success, JsMetalineSelectionRange? Selection, ReasonBase? FailReason);
	public record JsMetalineSelectionRange(Guid Metaline, int? LineId, int? LineIndex, SimpleRange Range);
}
