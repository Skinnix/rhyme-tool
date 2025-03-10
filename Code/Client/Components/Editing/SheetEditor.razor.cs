﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Data.Notation.Display;
using Skinnix.RhymeTool.Data;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Components;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Editing;

namespace Skinnix.RhymeTool.Client.Components.Editing;

partial class SheetEditor
{
	public static readonly Reason UnknownEditType = new("Unbekannter Bearbeitungstyp");
	public static readonly Reason MultilineEditNotSupported = new("Mehrzeilige Bearbeitung wird noch nicht unterstützt");
	public static readonly Reason LineNotFound = new("Zeile nicht gefunden");
	public static readonly Reason UndoNotAvailable = new("Rückgängig nicht möglich");
	public static readonly Reason RedoNotAvailable = new("Wiederholen nicht möglich");

	[Parameter] public SheetDocument? Document { get; set; }
	[Parameter] public ISheetEditorFormatter? Formatter { get; set; }
	[Parameter] public DocumentEditHistory? EditHistory { get; set; }

	private SheetDocument? loadedDocument;
	private ISheetEditorFormatter? loadedFormatter;

	private ElementReference editorWrapper;
	private RerenderAnchor rerenderAnchor = null!;
	private IJSObjectReference? jsEditor = null;

	private bool shouldRender;

	private readonly WeakDictionary<Guid, SheetDisplayLine[]> renderedLines = new();

	protected override void OnParametersSet()
	{
		base.OnParametersSet();

		if (Document != loadedDocument)
		{
			shouldRender = true;

			if (loadedDocument is not null)
				loadedDocument.Lines.Modified -= OnLinesModified;

			renderedLines.Clear();
			loadedDocument = Document;

			if (loadedDocument is not null)
				loadedDocument.Lines.Modified += OnLinesModified;
		}

		if (Formatter != loadedFormatter)
		{
			shouldRender = true;
			rerenderAnchor?.TriggerRender();

			loadedFormatter = Formatter;
		}
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		Console.WriteLine("render");

		if (firstRender)
		{
			jsEditor = await js.InvokeAsync<IJSObjectReference>("registerChordEditor", editorWrapper, DotNetObjectReference.Create(this), nameof(OnBeforeInput));
			//rerenderAnchor.BeforeRender = () => reference.InvokeVoidAsync("notifyBeforeRender");
			rerenderAnchor.AfterRender = () => jsEditor.InvokeVoidAsync("notifyAfterRender");
		}

		//await js.InvokeVoidAsync("notifyRenderFinished", this.GetType().Name);
		shouldRender = false;
	}

	public async ValueTask DisposeAsync()
	{
		if (jsEditor is not null)
		{
			await jsEditor.InvokeVoidAsync("destroy");
			await jsEditor.DisposeAsync();
			jsEditor = null;
		}
	}

	protected override bool ShouldRender()
	{
		if (!shouldRender)
			return false;

		return true;
	}

	private new void StateHasChanged()
	{
		shouldRender = true;
		base.StateHasChanged();
	}

	private void OnLinesModified(object? sender, ModifiedEventArgs args)
		=> StateHasChanged();

	private void OnLinesRendered(SheetDisplayLine[] lines)
	{
		if (lines.Length == 0)
			return;

		var lineGuid = lines[0].Editing.Line.Guid;
		renderedLines[lineGuid] = lines;
	}

	[JSInvokable]
	public JsMetalineEditResult OnBeforeInput(InputEventData data)
	{
		if (Document is null) throw new InvalidOperationException("Editor nicht initialisiert");

		//History?
		if (data.InputType == "historyUndo")
			return Undo();
		if (data.InputType == "historyRedo")
			return Redo();

		//Sind Start- und Endzeile unterschiedlich?
		MetalineSelectionRange? newSelection = null;
		if (data.Selection.Start.Metaline != data.Selection.End.Metaline
			|| data.Selection.Start.Line != data.Selection.End.Line)
		{
			//Mehrere Zeilen bearbeiten
			var editResult = EditMultiLine(data);

			if (!editResult.Success)
				return new JsMetalineEditResult(false, false, null, editResult.FailReason);

			//Neue Auswahl
			newSelection = editResult.NewSelection;

			//Speichere die Änderung
			EditHistory?.StoreEdit(GetEditLabel(data), newSelection, true, null);
		}
		else
		{
			//Einzelne Zeile bearbeiten
			var editResult = EditLine(data);

			if (!editResult.Success)
				return new JsMetalineEditResult(false, false, null, editResult.FailReason);

			//Neue Auswahl
			newSelection = editResult.NewSelection;

			//Speichere die Änderung
			EditHistory?.StoreEdit(GetEditLabel(data), newSelection, true, (data.Selection.Start.Metaline, data.Selection.Start.Line));
		}

		//Erzeuge Ergebnis
		var selection = LineSelection.FromRange(newSelection);
		rerenderAnchor.TriggerRender();
		return new JsMetalineEditResult(true, true, selection, null);
	}

	private JsMetalineEditResult Undo()
	{
		if (Document is null || EditHistory is null) throw new InvalidOperationException("Editor nicht initialisiert");

		if (!EditHistory.CanUndo)
			return new JsMetalineEditResult(false, false, null, UndoNotAvailable);

		var newSelection = EditHistory.Undo();
		var selection = LineSelection.FromRange(newSelection);
		rerenderAnchor.TriggerRender();
		return new JsMetalineEditResult(true, true, selection, null);
	}

	private JsMetalineEditResult Redo()
	{
		if (Document is null || EditHistory is null) throw new InvalidOperationException("Editor nicht initialisiert");

		if (!EditHistory.CanRedo)
			return new JsMetalineEditResult(false, false, null, RedoNotAvailable);

		var newSelection = EditHistory.Redo();
		var selection = LineSelection.FromRange(newSelection);
		rerenderAnchor.TriggerRender();
		return new JsMetalineEditResult(true, true, selection, null);
	}

	private string GetEditLabel(InputEventData data) => GetEditType(data) switch
	{
		EditType.InsertContent or EditType.InsertCompositionContent => "Einfügen",
		EditType.InsertLine => "Zeile einfügen",
		EditType.DeleteBackward or EditType.DeleteForward or EditType.DeleteWordBackward or EditType.DeleteWordForward => "Löschen",
		_ => "Bearbeitung"
	};

	protected MetalineEditResult EditLine(InputEventData data)
	{
		if (Document is null) throw new InvalidOperationException("Editor nicht initialisiert");

		//Finde die Displayzeile
		var line = FindLine(data.Selection.Start.Metaline, data.Selection.Start.Line);
		if (line is null)
			return MetalineEditResult.Fail(LineNotFound);

		//Auswahl
		var lineContext = Document.Lines.GetContextFor(line.Editing.Line)
			?? throw new InvalidOperationException("Kein Kontext");
		var selectionRange = new SimpleRange(data.Selection.Start.Offset, data.Selection.End.Offset);
		SimpleRange? editRange = data.EditRange is null ? null : new SimpleRange(data.EditRange.Start.Offset, data.EditRange.End.Offset);
		var context = new SheetDisplayLineEditingContext(lineContext, selectionRange, editRange, data.JustSelected, data.AfterCompose)
		{
			GetLineBefore = () => Document.Lines.GetLineBefore(line.Editing.Line),
			GetLineAfter = () => Document.Lines.GetLineAfter(line.Editing.Line),
		};

		//Bearbeitungstyp
		var editResult = GetEditType(data) switch
		{
			EditType.InsertContent or EditType.InsertCompositionContent when (data.Data is not null) => line.Editing.InsertContent(context, null, data.Data, Formatter),
			EditType.InsertLine => line.Editing.InsertContent(context, null, "\n", Formatter),
			EditType.DeleteBackward => line.Editing.DeleteContent(context, null, DeleteDirection.Backward, DeleteType.Character, Formatter),
			EditType.DeleteForward => line.Editing.DeleteContent(context, null, DeleteDirection.Forward, DeleteType.Character, Formatter),
			EditType.DeleteWordBackward => line.Editing.DeleteContent(context, null, DeleteDirection.Backward, DeleteType.Word, Formatter),
			EditType.DeleteWordForward => line.Editing.DeleteContent(context, null, DeleteDirection.Forward, DeleteType.Word, Formatter),
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

		//Finde die Displayzeilen
		var startLine = FindLine(data.Selection.Start.Metaline, data.Selection.Start.Line);
		var endLine = FindLine(data.Selection.End.Metaline, data.Selection.End.Line);
		if (startLine is null || endLine is null)
			return MultilineEditResult.Fail(LineNotFound);

		//Erzeuge Kontext
		var context = new SheetDisplayMultiLineEditingContext(Document, startLine.Editing, data.Selection.Start.Offset,
			endLine.Editing, data.Selection.End.Offset, data.JustSelected, data.AfterCompose);

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
			EditType.InsertContent or EditType.InsertCompositionContent when(data.Data is not null) => context.InsertContent(data.Data, Formatter),
			EditType.InsertLine => context.InsertContent("\n", Formatter),
			EditType.DeleteBackward or EditType.DeleteWordBackward => context.DeleteContent(DeleteDirection.Backward, Formatter),
			EditType.DeleteForward or EditType.DeleteWordForward => context.DeleteContent(DeleteDirection.Forward, Formatter),
			_ => MultilineEditResult.Fail(UnknownEditType)
		};

		return editResult;
	}

	private static EditType? GetEditType(InputEventData data) => data.InputType switch
	{
		"insertFromDrop" or "insertFromPaste" or "insertFromPasteAsQuotation" or "insertLink" or "insertText" => EditType.InsertContent,
		"insertCompositionText" => EditType.InsertCompositionContent,
		"insertLineBreak" or "insertParagraph" => EditType.InsertLine,
		"deleteByCut" or "deleteByDrag" or "deleteContentBackward" or "deleteContent" => EditType.DeleteBackward,
		"deleteContentForward" => EditType.DeleteForward,
		"deleteWord" or "deleteWordBackward" => EditType.DeleteWordBackward,
		"deleteWordForward" => EditType.DeleteWordForward,
		_ => null,
	};

	private SheetDisplayLine? FindLine(Guid metalineId, int lineId)
	{
		if (!renderedLines.TryGetValue(metalineId, out var lines))
			return null;
		return lines.FirstOrDefault(l => l.Id == lineId);
	}

	private enum EditType
	{
		InsertContent,
		InsertCompositionContent,
		InsertLine,
		DeleteBackward,
		DeleteForward,
		DeleteWordForward,
		DeleteWordBackward,
	}

	public record JsMetalineEditResult(bool Success, bool WillRender, LineSelection? Selection, ReasonBase? FailReason);
}
