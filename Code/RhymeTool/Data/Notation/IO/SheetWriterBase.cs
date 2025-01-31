namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetWriterBase
{
	public abstract void WriteSheet(Stream stream, SheetDocument document, bool leaveOpen = false);
	public abstract Task WriteSheetAsync(Stream stream, SheetDocument document, bool leaveOpen = false, CancellationToken cancellation = default);
}
