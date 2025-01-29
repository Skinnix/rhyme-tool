
namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetEncoderBase<TLine>(ISheetBuilderFormatter formatter)
{
	public ISheetBuilderFormatter Formatter => formatter;

	public abstract IEnumerable<TLine?> ProcessLines(SheetDocument document);
}
