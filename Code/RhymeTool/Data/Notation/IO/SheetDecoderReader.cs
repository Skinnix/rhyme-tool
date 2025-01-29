using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetDecoderReader : SheetReaderBase
{
	public static SheetDecoderStringReader<DefaultSheetDecoder> Default { get; } = new();
}

public abstract class SheetDecoderReader<TLine> : SheetDecoderReader
{
	protected virtual void ProcessLine(SheetDecoderBase<TLine> decoder, TLine line)
	{
		//Lese die Zeile
		decoder.ProcessLine(line);
	}

	protected virtual SheetDocument Finalize(SheetDecoderBase<TLine> decoder)
	{
		//Finalisiere das Dokument
		return decoder.Finalize();
	}
}

public abstract class SheetDecoderReader<TLine, TDecoder> : SheetDecoderReader<TLine>
	where TDecoder : SheetDecoderBase<TLine>
{

}

public abstract class SheetDecoderReader<TLine, TDecoder, TState> : SheetDecoderReader<TLine>
	where TDecoder : SheetDecoderBase<TLine>
	where TLine : notnull
{
	public override SheetDocument ReadSheet(Stream stream, bool leaveOpen = false)
	{
		var state = Open(stream, leaveOpen);
		try
		{
			return ReadSheet(state, GetDecoder());
		}
		finally
		{
			(state as IDisposable)?.Dispose();
		}
	}

	public override async Task<SheetDocument> ReadSheetAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
	{
		var state = await OpenAsync(stream, leaveOpen, cancellation);
		try
		{
			return await ReadSheetAsync(state, GetDecoder(), cancellation);
		}
		finally
		{
			if (state is IAsyncDisposable asyncDisposable)
				await asyncDisposable.DisposeAsync();
			else if (state is IDisposable disposable)
				disposable.Dispose();
		}
	}

	protected abstract TDecoder GetDecoder();

	protected abstract TState Open(Stream stream, bool leaveOpen = false);
	protected abstract Task<TState> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default);

	protected abstract TLine? TryReadLine(TState state);
	protected abstract Task<TLine?> TryReadLineAsync(TState state, CancellationToken cancellation = default);

	protected virtual SheetDocument ReadSheet(TState state, TDecoder decoder)
	{
		//Lese das Dokument zeilenweise
		for (var line = TryReadLine(state); line != null; line = TryReadLine(state))
			ProcessLine(decoder, line);

		//Finalisiere das Dokument
		return Finalize(decoder);
	}

	protected virtual async Task<SheetDocument> ReadSheetAsync(TState state, TDecoder decoder, CancellationToken cancellation = default)
	{
		//Lese das Dokument zeilenweise
		cancellation.ThrowIfCancellationRequested();
		for (var line = await TryReadLineAsync(state, cancellation); line != null; line = await TryReadLineAsync(state, cancellation))
		{
			cancellation.ThrowIfCancellationRequested();

			//Lese die Zeile
			ProcessLine(decoder, line);
		}

		//Finalisiere das Dokument
		cancellation.ThrowIfCancellationRequested();
		return Finalize(decoder);
	}
}
