using System.Runtime.Serialization;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetDecoderBase(ISheetEditorFormatter formatter)
{
	public ISheetEditorFormatter Formatter => formatter;

	public abstract void ProcessLine(string line);
	public abstract IEnumerable<SheetLine> Finalize();
}
