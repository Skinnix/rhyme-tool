using System.Text;

namespace Skinnix.RhymeTool.Client.IO;

public class SheetEncoderStringWriter<TEncoder> : SheetEncoderWriter<string, TEncoder, TextWriter>
	where TEncoder : SheetEncoderBase<string>, new()
{
	public Encoding Encoding { get; set; } = Encoding.UTF8;

	protected override TEncoder GetEncoder() => new();

	protected override TextWriter Open(Stream stream, bool leaveOpen = false) => new StreamWriter(stream, Encoding, leaveOpen: leaveOpen);
	protected override Task<TextWriter> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
		=> Task.FromResult(Open(stream, leaveOpen));

	protected override void WriteLine(TextWriter writer, string? line) => writer.WriteLine(line);
	protected override Task WriteLineAsync(TextWriter writer, string? line, CancellationToken cancellation = default)
		=> writer.WriteLineAsync(line.AsMemory(), cancellation);

	protected override void Finalize(TextWriter state, TEncoder encoder) { }
	protected override Task FinalizeAsync(TextWriter state, TEncoder encoder, CancellationToken cancellation = default) => Task.CompletedTask;
}
