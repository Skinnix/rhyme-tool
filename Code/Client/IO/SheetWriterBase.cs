using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public abstract class SheetWriterBase
{
	public abstract void WriteSheet(Stream stream, SheetDocument document, bool leaveOpen = false);
	public abstract Task WriteSheetAsync(Stream stream, SheetDocument document, bool leaveOpen = false, CancellationToken cancellation = default);
}
