//using Skinnix.RhymeTool.ComponentModel;
//using Skinnix.RhymeTool.Data.Notation.Display;

//namespace Skinnix.RhymeTool.Data.Notation;

//public class SheetSegmentTitleLine : SheetLine
//{
//	private readonly ISheetDisplayLineEditing editing;

//	public string Title { get; set; } = string.Empty;

//	public SheetSegmentTitleLine()
//	{
//		editing = new Editing(this);
//	}

//	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
//		=> [new SheetDisplaySegmentTitleLine(1, Title)
//		{
//			Editing = editing
//		}];

//	private class Editing : ISheetDisplayLineEditing
//	{
//		public SheetSegmentTitleLine Line { get; }
//		SheetLine ISheetDisplayLineEditing.Line => Line;

//		public int LineId => 0;

//		public Editing(SheetSegmentTitleLine owner)
//		{
//			this.Line = owner;
//		}

//		public MetalineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false)
//		{
//			//Ist der Bereich leer?
//			if (selectionRange.Length == 0)
//			{
//				if (forward)
//					selectionRange = new SimpleRange(selectionRange.Start, selectionRange.Start + 1);
//				else
//					selectionRange = new SimpleRange(selectionRange.Start - 1, selectionRange.Start);
//			}

//			return DeleteAndInsertContent(selectionRange, formatter, null);
//		}

//		public MetalineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter)
//		{
//			return DeleteAndInsertContent(selectionRange, formatter, content);
//		}

//		private MetalineEditResult DeleteAndInsertContent(SimpleRange selectionRange, ISheetFormatter? formatter, string? content)
//		{
//			if (selectionRange.Start < 1 || selectionRange.End > Line.Title.Length - 1)
//				return MetalineEditResult.Fail;

//			//Setze Titel
//			var newTitle = Line.Title.Remove(selectionRange.Start - 1, selectionRange.Length);
//			Line.Title = newTitle;

//			//Modified-Event
//			Line.RaiseModified(new ModifiedEventArgs(Line));
//			return new MetalineEditResult(true, Line.Guid, 0, new SimpleRange(selectionRange.Start, selectionRange.Start));
//		}
//	}
//}