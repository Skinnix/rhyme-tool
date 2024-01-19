using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public abstract class SheetLineComponent : DeepObservableBase
{
	public bool IsSelected { get; set; }
}

public record SheetLineComponentCutResult(bool Success)
{

}

public record SheetLineComponentReplaceResult(bool Success)
{
	
}

public sealed class SheetSpace : SheetCompositeLineComponent, ISheetDisplayLineElementSource
{
	private int length;
	public int Length
	{
		get => length;
		set => Set(ref length, value);
	}

	public SheetSpace(int length = 1)
	{
		this.length = length;
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
		=> new SheetCompositeLineBlock[]
		{
			new SheetCompositeLineBlock(
				new SheetCompositeLineBlockRow<SheetDisplayTextLine.Builder>(
					new SheetDisplayLineSpace(this, Length)))
		};

	//public override SheetLineComponentCutResult CutContent(SimpleRange range, ISheetFormatter? formatter)
	//{
	//	if (Length <= range.Length)
	//		return new SheetLineComponentCutResult(false);

	//	Length -= range.Length;
	//	return new SheetLineComponentCutResult(true);
	//}

	internal override SheetCompositeLineComponentOptimizationResult Optimize(ISheetFormatter? formatter)
	{
		return new SheetCompositeLineComponentOptimizationResult()
		{
			RemoveComponent = Length <= 0
		};
	}
}