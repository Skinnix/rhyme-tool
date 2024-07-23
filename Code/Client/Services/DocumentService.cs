using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.Services;

public interface IDocumentService
{
	Task<IDocumentSource?> TryGetDocument(string? id);

	void SetCurrentDocument(IDocumentSource? source);
	Task<IDocumentSource> LoadFile(IFileContent file);
}

public interface IDocumentSource
{
	bool CanLoad { get; }
	bool CanSave { get; }
	string? Id { get; }

	Task<SheetDocument> LoadAsync(bool reload = false, CancellationToken cancellation = default);

	Task SaveAsync(SheetDocument document, CancellationToken cancellation = default);
}

internal class DefaultDocumentService : IDocumentService
{
	private IDocumentSource? currentDocument;

	public Task<IDocumentSource?> TryGetDocument(string? id)
	{
		if (currentDocument is not null && currentDocument.Id == id)
			return Task.FromResult<IDocumentSource?>(currentDocument);

		return Task.FromResult<IDocumentSource?>(null);
	}

	public void SetCurrentDocument(IDocumentSource? source)
	{
		currentDocument = source;
	}

	public Task<IDocumentSource> LoadFile(IFileContent file)
		=> Task.FromResult<IDocumentSource>(new FileDocumentSource(file));

	private class FileDocumentSource(IFileContent file) : IDocumentSource
	{
		//private SheetDocument? document;

		public bool CanLoad => file.CanRead;
		public bool CanSave => file.CanWrite;

		public string? Id => file.Id;

		public async Task<SheetDocument> LoadAsync(bool reload = false, CancellationToken cancellation = default)
		{
			cancellation.ThrowIfCancellationRequested();
			//if (!reload && document is not null)
			//	return document;

			using (var stream = await file.ReadAsync(cancellation))
			using (var reader = new StreamReader(stream))
			{
				var document = await SheetReader.ReadSheetAsync(reader, cancellation);
				document.Label = file.Name;
				return document;
			}
		}

		public async Task SaveAsync(SheetDocument document, CancellationToken cancellation = default)
		{
			cancellation.ThrowIfCancellationRequested();
			await file.WriteAsync(async stream =>
			{
				using (var writer = new StreamWriter(stream))
				{
					await SheetWriter.WriteSheetAsync(writer, document, cancellation);
				}
			}, cancellation);

			//this.document = document;
		}
	}
}
