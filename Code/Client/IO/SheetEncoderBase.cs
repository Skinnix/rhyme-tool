using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public abstract class SheetEncoderBase<TLine>(ISheetBuilderFormatter formatter)
{
	public ISheetBuilderFormatter Formatter => formatter;

	public abstract IEnumerable<TLine?> ProcessLines(SheetDocument document);
}
