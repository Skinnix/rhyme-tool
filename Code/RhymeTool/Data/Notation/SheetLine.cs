using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public abstract class SheetLine : DeepObservableBase
{
	public Guid Guid { get; set; } = Guid.NewGuid();

    public abstract IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null);
    //public abstract IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null);
}

public class SheetEmptyLine : SheetLine, ISheetDisplayLineEditing
{
	private int count = 1;
	public int Count
	{
		get => count;
		set => Set(ref count, value);
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null)
    {
        for (int i = 0; i < Count; i++)
		{
			yield return new SheetDisplayEmptyLine()
			{
				Editing = this
			};
		}
    }

	//public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null)
	//{
	//    yield return new SheetDisplayContentBlock(new SheetDisplayEmptyLine());
	//}

	public LineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false) => throw new NotImplementedException();
	public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter) => throw new NotImplementedException();
}