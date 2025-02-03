using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Editing;

public class DocumentEditHistory
{
	private readonly SheetDocument document;
	private readonly SheetDocument.Stored baseState;
	private readonly List<Entry> entries = new();

	private int redoIndex = -1;

	public DocumentEditHistory(SheetDocument document)
	{
		this.document = document;
		baseState = document.Store();
	}

	public void StoreEdit(string label, MetalineSelectionRange selection, bool tryAppend)
	{
		var entrySelection = new Selection(
			new Selection.Anchor(selection.StartMetaline.Guid, selection.StartLineId, selection.Range.Start),
			new Selection.Anchor(selection.EndMetaline.Guid, selection.EndLineId, selection.Range.End)
		);

		var store = document.Store();
		store = entries.Count == 0 ? store.OptimizeWith(baseState) : store.OptimizeWith(entries[^1].Store);

		if (redoIndex >= 0)
		{
			entries.RemoveRange(redoIndex + 1, entries.Count - redoIndex - 1);
			redoIndex = -1;
			entries[^1] = new Entry(store, label, entrySelection);
		}
		else
		{
			var newEntry = new Entry(store, label, entrySelection);
			entries.Add(newEntry);
		}

		if (tryAppend && entries.Count > 1)
		{
			var last = entries[^2];
			if (last.Label != label)
				return;

			entries.RemoveAt(entries.Count - 2);
		}
	}

	public bool CanUndo => entries.Count > 0 && redoIndex != 0;
	public bool CanRedo => redoIndex != -1;

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

			return new MetalineSelectionRange(line, SimpleRange.CursorAtStart, line.FirstExistingLineId);
		}

		var entry = entries[redoIndex - 1];
		entry.Store.Apply(document);

		var startLine = document.Lines.FirstOrDefault(l => l.Guid == entry.Selection.Start.Metaline);
		var endLine = entry.Selection.Start.Metaline == entry.Selection.End.Metaline ? startLine
			: document.Lines.FirstOrDefault(l => l.Guid == entry.Selection.End.Metaline);

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

		internal Entry(SheetDocument.Stored store, string label, Selection selection)
		{
			Label = label;
			this.Store = store;
			Selection = selection;
		}
	}

	private readonly record struct Selection(Selection.Anchor Start, Selection.Anchor End)
	{
		public readonly record struct Anchor(Guid Metaline, int Line, int Offset);
	}
}
