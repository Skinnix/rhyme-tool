namespace Skinnix.RhymeTool.Data.Notation.Display;

public record struct SheetDisplaySliceInfo(int ComponentIndex, int BlockIndex, int SliceIndex, int ContentOffset)
{
	public int VirtualOffset { get; init; }
	public int VirtualLength { get; init; }

	public bool IsVirtual => VirtualOffset != 0 || VirtualLength != 0;
}
