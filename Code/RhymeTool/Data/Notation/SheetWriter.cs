namespace Skinnix.RhymeTool.Data.Notation;

public class SheetWriter
{
	private readonly TextWriter writer;
	private readonly SheetDocument document;

	public ISheetBuilderFormatter? Formatter { get; set; } = DefaultSheetFormatter.Instance;

	private SheetWriter(TextWriter writer, SheetDocument document)
	{
		this.writer = writer;
		this.document = document;
	}

	public static void WriteSheet(TextWriter writer, SheetDocument document)
		=> new SheetWriter(writer, document).WriteSheet();
	public static Task WriteSheetAsync(TextWriter writer, SheetDocument document, CancellationToken cancellation = default)
		=> new SheetWriter(writer, document).WriteSheetAsync(cancellation);

	private void WriteSheet()
	{
		foreach (var line in document.Lines)
			WriteTextLine(line);
	}

	private async Task WriteSheetAsync(CancellationToken cancellation = default)
	{
		foreach (var line in document.Lines)
			await WriteTextLineAsync(line, cancellation);
	}

	private void WriteTextLine(SheetLine line)
	{
		var displayLines = line.CreateDisplayLines();
		foreach (var displayLine in displayLines)
		{
			foreach (var element in displayLine.GetElements())
			{
				var elementContent = element.ToString(Formatter);
				writer.Write(elementContent);
			}

			writer.WriteLine();
		}
	}

	private async Task WriteTextLineAsync(SheetLine line, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		var displayLines = line.CreateDisplayLines(Formatter);
		foreach (var displayLine in displayLines)
		{
			foreach (var element in displayLine.GetElements())
			{
				cancellation.ThrowIfCancellationRequested();
				var elementContent = element.ToString(Formatter);
				await writer.WriteAsync(elementContent);
			}

			cancellation.ThrowIfCancellationRequested();
			await writer.WriteLineAsync();
		}
	}
}
