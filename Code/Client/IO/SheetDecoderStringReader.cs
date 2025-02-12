using System.Text;

namespace Skinnix.RhymeTool.Client.IO;

public class SheetDecoderStringReader<TDecoder> : SheetDecoderReader<string, TDecoder, TextReader>
	where TDecoder : SheetDecoderBase<string>, new()
{
	public Encoding Encoding { get; set; } = Encoding.UTF8;

	protected override TDecoder GetDecoder() => new();

	protected override TextReader Open(Stream stream, bool leaveOpen = false) => new StreamReader(stream, Encoding, leaveOpen: leaveOpen);
	protected override Task<TextReader> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
		=> Task.FromResult(Open(stream, leaveOpen));

	protected override string? TryReadLine(TextReader reader) => reader.ReadLine();
	protected override Task<string?> TryReadLineAsync(TextReader reader, CancellationToken cancellation = default) => reader.ReadLineAsync(cancellation).AsTask();

	protected override void ProcessLine(SheetDecoderBase<string> decoder, string line)
	{
		//Schneide Zeilenumbrüche ab
		if (line.StartsWith("\r\n"))
			line = line[2..];
		else if (line.StartsWith("\n"))
			line = line[1..];
		else if (line.StartsWith("\r"))
			line = line[1..];

		//Lese die Zeile
		base.ProcessLine(decoder, line);
	}
}
