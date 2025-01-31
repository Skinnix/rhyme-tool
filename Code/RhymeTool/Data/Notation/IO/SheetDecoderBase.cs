using System.Runtime.Serialization;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetDecoderBase<TLine>(ISheetEditorFormatter formatter)
{
	public ISheetEditorFormatter Formatter => formatter;

	public abstract void ProcessLine(TLine line);
	public abstract SheetDocument Finalize();
}
