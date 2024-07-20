using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.Services;

public class WorkSession : DeepObservableBase
{
	private WorkDocument? currentDocument;
	public WorkDocument? CurrentDocument
	{
		get => currentDocument;
		set => Set(ref currentDocument, value);
	}

	public async Task OpenDocument(Stream stream, IFileContent? source)
	{
		SheetDocument document;
		using (var streamReader = new StreamReader(stream))
		{
			document = await SheetReader.ReadSheetAsync(streamReader);
		}

		CurrentDocument = new WorkDocument(document, source);
	}

	public record WorkDocument(SheetDocument Document, IFileContent? Source);
}
