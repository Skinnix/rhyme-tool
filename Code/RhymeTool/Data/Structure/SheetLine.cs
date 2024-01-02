using Skinnix.RhymeTool.Data.Structure.Display;

namespace Skinnix.RhymeTool.Data.Structure;

public abstract class SheetLine
{
	public abstract IEnumerable<SheetDisplayLine> CreateDisplayLines();
	public abstract IEnumerable<SheetDisplayBlock> CreateDisplayBlocks();
}

public class SheetEmptyLine : SheetLine
{
	public int Count { get; set; } = 1;

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines()
	{
		for (int i = 0; i < Count; i++)
			yield return new SheetDisplayEmptyLine();
	}

	public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks()
	{
		yield return new SheetDisplayContentBlock(new SheetDisplayEmptyLine());
	}
}