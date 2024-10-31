using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public class SheetDecoderReader
{
	public static SheetDecoderReader<DefaultSheetDecoder> Default { get; } = new();

	public SheetDocument ReadSheet(TextReader reader, SheetDecoderBase decoder)
	{
		//Lese das Dokument zeilenweise
		for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
			ProcessLine(decoder, line);

		//Finalisiere das Dokument
		return Finalize(decoder);
	}

	public async Task<SheetDocument> ReadSheetAsync(TextReader reader, SheetDecoderBase decoder, CancellationToken cancellation = default)
	{
		//Lese das Dokument zeilenweise
		cancellation.ThrowIfCancellationRequested();
		for (var line = await reader.ReadLineAsync(cancellation); line != null; line = await reader.ReadLineAsync(cancellation))
		{
			cancellation.ThrowIfCancellationRequested();

			//Lese die Zeile
			ProcessLine(decoder, line);
		}

		//Finalisiere das Dokument
		cancellation.ThrowIfCancellationRequested();
		return Finalize(decoder);
	}

	protected virtual void ProcessLine(SheetDecoderBase decoder, string line)
	{
		//Schneide Zeilenumbrüche ab
		if (line.StartsWith("\r\n"))
			line = line[2..];
		else if (line.StartsWith("\n"))
			line = line[1..];
		else if (line.StartsWith("\r"))
			line = line[1..];

		//Lese die Zeile
		decoder.ProcessLine(line);
	}

	protected virtual SheetDocument Finalize(SheetDecoderBase decoder)
	{
		//Finalisiere das Dokument
		var lines = decoder.Finalize();
		return new SheetDocument(lines);
	}
}

public class SheetDecoderReader<TDecoder> : SheetDecoderReader
	where TDecoder : SheetDecoderBase, new()
{
	public SheetDocument ReadSheet(TextReader reader)
		=> ReadSheet(reader, new TDecoder());

	public Task<SheetDocument> ReadSheetAsync(TextReader reader, CancellationToken cancellation = default)
		=> ReadSheetAsync(reader, new TDecoder(), cancellation);
}
