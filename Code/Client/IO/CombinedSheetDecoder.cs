using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public class CombinedSheetDecoderReader : SheetDecoderReader
{
	public static new CombinedSheetDecoderReader Default { get; } = new();

	private readonly CpsSheetDecoderReader cpsReader = new();

	public override SheetDocument ReadSheet(Stream stream, bool leaveOpen = false)
	{
		using var reader = new StreamReader(stream, leaveOpen: leaveOpen);
		var firstLine = reader.ReadLine();
		if (firstLine is null)
			return new SheetDocument();

		var file = CpsFile.TryCreate(firstLine);
		if (file is not null)
			return ReadCpsSheet(reader, file);

		return ReadSimpleSheet(reader, firstLine);
	}

	public override async Task<SheetDocument> ReadSheetAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		using var reader = new StreamReader(stream, leaveOpen: leaveOpen);
		var firstLine = await reader.ReadLineAsync();
		if (firstLine is null)
			return new SheetDocument();

		cancellation.ThrowIfCancellationRequested();
		var file = CpsFile.TryCreate(firstLine);
		if (file is not null)
			return await ReadCpsSheetAsync(reader, file);

		return await ReadSimpleSheetAsync(reader, firstLine);
	}

	private SheetDocument ReadCpsSheet(StreamReader reader, CpsFile file)
		=> cpsReader.ReadSheet(reader, file, new(), new CpsSheetDecoder());

	private Task<SheetDocument> ReadCpsSheetAsync(StreamReader reader, CpsFile file)
		=> cpsReader.ReadSheetAsync(reader, file, new(), new CpsSheetDecoder());

	private SheetDocument ReadSimpleSheet(StreamReader reader, string firstLine)
	{
		//Lese das Dokument zeilenweise
		var decoder = new DefaultSheetDecoder(DefaultSheetFormatter.Instance);
		for (var line = firstLine; line != null; line = reader.ReadLine())
			ProcessSimpleLine(decoder, line);

		//Finalisiere das Dokument
		return decoder.Finalize();
	}

	private async Task<SheetDocument> ReadSimpleSheetAsync(StreamReader reader, string firstLine, CancellationToken cancellation = default)
	{
		//Lese das Dokument zeilenweise
		cancellation.ThrowIfCancellationRequested();
		var decoder = new DefaultSheetDecoder(DefaultSheetFormatter.Instance);
		for (var line = firstLine; line != null; line = await reader.ReadLineAsync(cancellation))
		{
			cancellation.ThrowIfCancellationRequested();

			//Lese die Zeile
			ProcessSimpleLine(decoder, line);
		}

		//Finalisiere das Dokument
		cancellation.ThrowIfCancellationRequested();
		return decoder.Finalize();
	}

	private void ProcessSimpleLine(SheetDecoderBase<string> decoder, string line)
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
}
