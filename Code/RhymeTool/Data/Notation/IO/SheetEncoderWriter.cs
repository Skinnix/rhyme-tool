
namespace Skinnix.RhymeTool.Data.Notation.IO;

public class SheetEncoderWriter
{
	public static SheetEncoderWriter<DefaultSheetEncoder> Default { get; } = new();

	public void WriteSheet(TextWriter writer, SheetEncoderBase encoder, SheetDocument document)
	{
		foreach (var part in encoder.ProcessLines(document))
		{
			if (part is null)
				writer.WriteLine();
			else
				writer.Write(part);
		}
	}

	public async Task WriteSheetAsync(TextWriter writer, SheetEncoderBase encoder, SheetDocument document, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		foreach (var part in encoder.ProcessLines(document))
		{
			cancellation.ThrowIfCancellationRequested();
			if (part is null)
				await writer.WriteLineAsync();
			else
				await writer.WriteAsync(part);
			cancellation.ThrowIfCancellationRequested();
		}
	}
}

public class SheetEncoderWriter<TEncoder> : SheetEncoderWriter
	where TEncoder : SheetEncoderBase, new()
{
	public void WriteSheet(TextWriter writer, SheetDocument document)
		=> WriteSheet(writer, new TEncoder(), document);

	public Task WriteSheetAsync(TextWriter writer, SheetDocument document, CancellationToken cancellation = default)
		=> WriteSheetAsync(writer, new TEncoder(), document, cancellation);
}
