using Skinnix.RhymeTool.Data.Structure.Display;

namespace Skinnix.RhymeTool.Data.Structure;

public abstract class SheetLine
{
	public abstract IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null);
	public abstract IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null);
}

public class SheetEmptyLine : SheetLine
{
	public int Count { get; set; } = 1;

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null)
	{
		for (int i = 0; i < Count; i++)
			yield return new SheetDisplayEmptyLine();
	}

	public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null)
	{
		yield return new SheetDisplayContentBlock(new SheetDisplayEmptyLine());
	}
}