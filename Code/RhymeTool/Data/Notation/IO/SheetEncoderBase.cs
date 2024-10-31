
namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetEncoderBase(ISheetBuilderFormatter formatter)
{
	public ISheetBuilderFormatter Formatter => formatter;

	public abstract IEnumerable<string?> ProcessLines(SheetDocument document);
}
