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
	string? Id { get; }
	Task<SheetDocument> LoadDocument(bool reload = false);
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
		private SheetDocument? document;

		public string? Id => file.Id;

		public async Task<SheetDocument> LoadDocument(bool reload = false)
		{
			if (!reload && document is not null)
				return document;

			using (var stream = await file.ReadAsync())
			using (var reader = new StreamReader(stream))
			{
				document = await SheetReader.ReadSheetAsync(reader);
				document.Label = file.Name;
				return document;
			}
		}
	}
}
