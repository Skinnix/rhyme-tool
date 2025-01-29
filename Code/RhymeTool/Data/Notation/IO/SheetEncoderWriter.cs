namespace Skinnix.RhymeTool.Data.Notation.IO;

public abstract class SheetEncoderWriter : SheetWriterBase
{
	public static SheetEncoderStringWriter<DefaultSheetEncoder> Default { get; } = new();
}

public abstract class SheetEncoderWriter<TLine> : SheetEncoderWriter
{
	protected virtual IEnumerable<TLine?> ProcessLines(SheetEncoderBase<TLine> encoder, SheetDocument document)
		=> encoder.ProcessLines(document);
}

public abstract class SheetEncoderWriter<TLine, TEncoder> : SheetEncoderWriter<TLine>
	where TEncoder : SheetEncoderBase<TLine>
{
}

public abstract class SheetEncoderWriter<TLine, TEncoder, TState> : SheetEncoderWriter<TLine, TEncoder>
	where TEncoder : SheetEncoderBase<TLine>
{
	public override void WriteSheet(Stream stream, SheetDocument document, bool leaveOpen = false)
	{
		var state = Open(stream, leaveOpen);
		try
		{
			WriteSheet(state, GetEncoder(), document);
		}
		finally
		{
			(state as IDisposable)?.Dispose();
		}
	}

	public override async Task WriteSheetAsync(Stream stream, SheetDocument document, bool leaveOpen = false, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		var state = await OpenAsync(stream, leaveOpen, cancellation);
		cancellation.ThrowIfCancellationRequested();

		try
		{
			await WriteSheetAsync(state, GetEncoder(), document, cancellation);
		}
		finally
		{
			if (state is IAsyncDisposable asyncDisposable)
				await asyncDisposable.DisposeAsync();
			else if (state is IDisposable disposable)
				disposable.Dispose();
		}
	}

	protected abstract TEncoder GetEncoder();

	protected abstract TState Open(Stream stream, bool leaveOpen = false);
	protected abstract Task<TState> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default);

	protected abstract void WriteLine(TState state, TLine? line);
	protected abstract Task WriteLineAsync(TState state, TLine? line, CancellationToken cancellation = default);

	protected abstract void Finalize(TState state, TEncoder encoder);
	protected abstract Task FinalizeAsync(TState state, TEncoder encoder, CancellationToken cancellation = default);

	public virtual void WriteSheet(TState state, TEncoder encoder, SheetDocument document)
	{
		foreach (var line in ProcessLines(encoder, document))
		{
			WriteLine(state, line);
		}

		Finalize(state, encoder);
	}

	public async Task WriteSheetAsync(TState state, TEncoder encoder, SheetDocument document, CancellationToken cancellation = default)
	{
		foreach (var line in ProcessLines(encoder, document))
		{
			cancellation.ThrowIfCancellationRequested();
			await WriteLineAsync(state, line, cancellation);
			cancellation.ThrowIfCancellationRequested();
		}

		cancellation.ThrowIfCancellationRequested();
		await FinalizeAsync(state, encoder, cancellation);
	}
}
