namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetReaderBase
{
	public abstract SheetDocument ReadSheet(Stream stream, bool leaveOpen = false);
	public abstract Task<SheetDocument> ReadSheetAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default);
}
