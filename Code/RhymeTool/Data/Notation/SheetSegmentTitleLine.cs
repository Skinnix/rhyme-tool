using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetSegmentTitleLine : SheetLine
{
	private readonly ISheetDisplayLineEditing editing;

	public string Title { get; set; } = string.Empty;

	public SheetSegmentTitleLine()
	{
		editing = new Editing(this);
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
		=> [new SheetDisplaySegmentTitleLine(Title)
		{
			Editing = editing
		}];

	private class Editing : ISheetDisplayLineEditing
	{
		private readonly SheetSegmentTitleLine owner;

		public Editing(SheetSegmentTitleLine owner)
		{
			this.owner = owner;
		}

		public LineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false)
		{
			//Ist der Bereich leer?
			if (selectionRange.Length == 0)
			{
				if (forward)
					selectionRange = new SimpleRange(selectionRange.Start, selectionRange.Start + 1);
				else
					selectionRange = new SimpleRange(selectionRange.Start - 1, selectionRange.Start);
			}

			return DeleteAndInsertContent(selectionRange, formatter, null);
		}

		public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter)
		{
			return DeleteAndInsertContent(selectionRange, formatter, content);
		}

		private LineEditResult DeleteAndInsertContent(SimpleRange selectionRange, ISheetFormatter? formatter, string? content)
		{
			if (selectionRange.Start < 1 || selectionRange.End > owner.Title.Length - 1)
				return new LineEditResult(false, null);

			//Setze Titel
			var newTitle = owner.Title.Remove(selectionRange.Start - 1, selectionRange.Length);
			owner.Title = newTitle;

			//Modified-Event
			owner.RaiseModified(new ModifiedEventArgs(owner));
			return new LineEditResult(true, new SimpleRange(selectionRange.Start, selectionRange.Start));
		}
	}
}