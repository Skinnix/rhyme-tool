using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Editing;

public class DocumentEditHistory
{
	public const int MAX_HISTORY_LENGTH = 300;

	public event EventHandler? HistoryChanged;

	private readonly SheetDocument document;
	private readonly SheetDocument.Stored baseState;
	private readonly List<Entry> entries = new();

	private readonly UndoCommandImpl undoCommand;
	private readonly RedoCommandImpl redoCommand;

	private int redoIndex = -1;

	public bool CanUndo => entries.Count > 0 && redoIndex != 0;
	public bool CanRedo => redoIndex != -1;

	public string? UndoLabel
		=> entries.Count == 0 ? null
		: redoIndex == 0 ? null
		: redoIndex == -1 ? entries[^1].Label
		: entries[redoIndex - 1].Label;

	public string? RedoLabel
		=> redoIndex == -1 ? null
		: entries[redoIndex].Label;

	public ICommand UndoCommand => undoCommand;
	public ICommand RedoCommand => redoCommand;

	public DocumentEditHistory(SheetDocument document)
	{
		this.document = document;
		baseState = document.Store();

		undoCommand = new UndoCommandImpl(this);
		redoCommand = new RedoCommandImpl(this);
	}

	private void InvokeHistoryChanged() => HistoryChanged?.Invoke(this, EventArgs.Empty);

	public void StoreEdit(string label, MetalineSelectionRange selection, bool tryAppend, (Guid Metaline, int Line)? editLineId)
	{
		var entrySelection = new Selection(
			new Selection.Anchor(selection.StartMetaline.Guid, selection.StartLineId, selection.Range.Start),
			new Selection.Anchor(selection.EndMetaline.Guid, selection.EndLineId, selection.Range.End)
		);

		var store = document.Store();

		if (redoIndex >= 0)
		{
			entries.RemoveRange(redoIndex + 1, entries.Count - redoIndex - 1);
			redoIndex = -1;
			entries[^1] = new Entry(store, label, entrySelection, editLineId);
		}
		else
		{
			var newEntry = new Entry(store, label, entrySelection, editLineId);
			entries.Add(newEntry);
		}

		if (tryAppend && entries.Count >= 2 && editLineId is not null && entries[^2].EditLineId == editLineId)
		{
			var last = entries[^2];
			if (last.Label == label)
			{
				entries.RemoveAt(entries.Count - 2);
			}
		}

		if (entries.Count > MAX_HISTORY_LENGTH)
			entries.RemoveRange(0, entries.Count - MAX_HISTORY_LENGTH);

		undoCommand.CanUndo = CanUndo;
		redoCommand.CanRedo = CanRedo;

		InvokeHistoryChanged();
	}

	public MetalineSelectionRange? Undo()
	{
		if (entries.Count == 0 || redoIndex == 0)
			return null;

		redoIndex = redoIndex == -1 ? entries.Count - 1 : redoIndex - 1;
		if (redoIndex == 0)
		{
			baseState.Apply(document);
			SheetLine line;
			if (document.Lines.Count == 0)
			{
				line = new SheetEmptyLine();
				document.Lines.Add(line);
			}
			else
			{
				line = document.Lines[0];
			}

			undoCommand.CanUndo = CanUndo;
			redoCommand.CanRedo = CanRedo;

			InvokeHistoryChanged();

			return new MetalineSelectionRange(line, SimpleRange.CursorAtStart, line.FirstExistingLineId);
		}

		var entry = entries[redoIndex - 1];
		entry.Store.Apply(document);

		var startLine = document.Lines.FirstOrDefault(l => l.Guid == entry.Selection.Start.Metaline);
		var endLine = entry.Selection.Start.Metaline == entry.Selection.End.Metaline ? startLine
			: document.Lines.FirstOrDefault(l => l.Guid == entry.Selection.End.Metaline);

		undoCommand.CanUndo = CanUndo;
		redoCommand.CanRedo = CanRedo;

		InvokeHistoryChanged();

		if (startLine is null || endLine is null)
			return null;

		return new MetalineSelectionRange(startLine, new SimpleRange(entry.Selection.Start.Offset, entry.Selection.End.Offset), entry.Selection.Start.Line)
		{
			EndMetaline = endLine,
			EndLineId = entry.Selection.End.Line,
		};
	}

	public MetalineSelectionRange? Redo()
	{
		if (!CanRedo)
			return null;

		var redoEntry = entries[redoIndex++];
		if (redoIndex == entries.Count)
			redoIndex = -1;

		redoEntry.Store.Apply(document);

		var startLine = document.Lines.FirstOrDefault(l => l.Guid == redoEntry.Selection.Start.Metaline);
		var endLine = redoEntry.Selection.Start.Metaline == redoEntry.Selection.End.Metaline ? startLine
			: document.Lines.FirstOrDefault(l => l.Guid == redoEntry.Selection.End.Metaline);

		undoCommand.CanUndo = CanUndo;
		redoCommand.CanRedo = CanRedo;

		InvokeHistoryChanged();

		if (startLine is null || endLine is null)
			return null;

		return new MetalineSelectionRange(startLine, new SimpleRange(redoEntry.Selection.Start.Offset, redoEntry.Selection.End.Offset), redoEntry.Selection.Start.Line)
		{
			EndMetaline = endLine,
			EndLineId = redoEntry.Selection.End.Line,
		};
	}

	private readonly record struct Entry
	{
		public SheetDocument.Stored Store { get; }
		public string Label { get; }
		public Selection Selection { get; }
		public (Guid Metaline, int Line)? EditLineId { get; }

		internal Entry(SheetDocument.Stored store, string label, Selection selection, (Guid Metaline, int Line)? editLineId)
		{
			Label = label;
			Store = store;
			Selection = selection;
			EditLineId = editLineId;
		}
	}

	private readonly record struct Selection(Selection.Anchor Start, Selection.Anchor End)
	{
		public readonly record struct Anchor(Guid Metaline, int Line, int Offset);
	}

	private class UndoCommandImpl(DocumentEditHistory history) : ICommand
	{
		public event EventHandler? CanExecuteChanged;

		private bool canUndo;
		public bool CanUndo
		{
			get => canUndo;
			set
			{
				if (canUndo == value)
					return;

				canUndo = value;
				CanExecuteChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public bool CanExecute(object? parameter) => canUndo;

		public void Execute(object? parameter)
		{
			if (!canUndo)
				return;

			history.Undo();
		}
	}

	private class RedoCommandImpl(DocumentEditHistory history) : ICommand
	{
		public event EventHandler? CanExecuteChanged;

		private bool canRedo;
		public bool CanRedo
		{
			get => canRedo;
			set
			{
				if (canRedo == value)
					return;
				canRedo = value;
				CanExecuteChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public bool CanExecute(object? parameter) => canRedo;

		public void Execute(object? parameter)
		{
			if (!canRedo)
				return;
			history.Redo();
		}
	}
}
