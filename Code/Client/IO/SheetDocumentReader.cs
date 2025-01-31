using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.IO;

namespace Skinnix.RhymeTool.Client.IO;

public static class SheetDocumentReader
{
	public static SheetDocument ReadSheet(Stream stream)
	{
		if (stream.CanSeek)
		{
			var result = stream.ReadByte();
			stream.Seek(0, SeekOrigin.Begin);

			if (result == '{')
			{
				return ChordProSheetDecoderReader.Default.ReadSheet(stream);
			}

			return SheetDecoderReader.Default.ReadSheet(stream);
		}
		else
		{
			using (var buffered = new PeekableStream(stream, 1))
			{
				var buffer = new byte[1];
				buffered.Peek(buffer, 0, 1);

				if (buffer[0] == '{')
				{
					return ChordProSheetDecoderReader.Default.ReadSheet(buffered);
				}

				return SheetDecoderReader.Default.ReadSheet(buffered);
			}
		}
	}

	public static async Task<SheetDocument> ReadSheetAsync(Stream stream, CancellationToken cancellation = default)
	{
		if (stream.CanSeek)
		{
			var buffer = new byte[1];
			await stream.ReadExactlyAsync(buffer, cancellation);
			stream.Seek(0, SeekOrigin.Begin);

			if (buffer[0] == '{')
			{
				return ChordProSheetDecoderReader.Default.ReadSheet(stream);
			}

			return SheetDecoderReader.Default.ReadSheet(stream);
		}
		else
		{
			using (var buffered = new PeekableStream(stream, 1))
			{
				var buffer = new byte[1];
				await buffered.PeekAsync(buffer, 0, 1);

				if (buffer[0] == '{')
				{
					return ChordProSheetDecoderReader.Default.ReadSheet(buffered);
				}

				return SheetDecoderReader.Default.ReadSheet(buffered);
			}
		}
	}
}
