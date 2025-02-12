using System.Runtime.Serialization;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public abstract class SheetDecoderBase<TLine>(ISheetEditorFormatter formatter)
{
	public ISheetEditorFormatter Formatter => formatter;

	public abstract void ProcessLine(TLine line);
	public abstract SheetDocument Finalize();
}
