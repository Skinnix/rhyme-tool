using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Client.Components.Editing;
using Skinnix.RhymeTool.Client.Components.Rendering;
using Skinnix.RhymeTool.Client.Components;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Editing;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.Pages;

partial class Document
{
	private static readonly bool IsDebug =
#if DEBUG
		true;
#else
        false;
#endif

	[Parameter, SupplyParameterFromQuery] public string? Doc { get; set; }

	public static string GetUrl(IDocumentSource document) => GetUrl(document.Id);
	public static string GetUrl(string? documentId) => documentId is null ? "/chords/document" : "/chords/document?doc=" + Uri.EscapeDataString(documentId);

	private ElementReference wrapper;
	private int? maxCharacters = null;
	private IJSObjectReference? resizer;

	private Guid inputFileId = Guid.NewGuid();

	private IDocumentSource? documentSource;
	private SheetDocument? document;
	private DocumentEditHistory? editHistory;

	private RenderingSettings renderingSettings = new();
	private EditingSettings editingSettings = new();

	private SheetEditor? editor;

	private ViewMode viewMode;

	private DocumentSettings CurrentSettings => viewMode switch
	{
		ViewMode.Editor => editingSettings,
		_ => renderingSettings,
	};

	protected override async Task OnParametersSetAsync()
	{
		await base.OnParametersSetAsync();

		renderingSettings.PropertyChanged -= OnSettingsPropertyChanged;
		renderingSettings = new()
		{
			FontSize = 100,
			Formatter = new DefaultSheetFormatter()
			{
				GermanMode = GermanNoteMode.Descriptive,
			}
		};
		renderingSettings.PropertyChanged += OnSettingsPropertyChanged;

		editingSettings.PropertyChanged -= OnSettingsPropertyChanged;
		editingSettings = new()
		{
			FontSize = 100,
			Formatter = new DefaultSheetFormatter()
			{
				GermanMode = GermanNoteMode.Descriptive,
				ExtendAttachmentLines = true,
				//CondenseTabNotes = false,
			},
		};

		documentSource = await documentService.TryGetDocument(Doc);
		if (documentSource is not null)
		{
			if (document is not null)
			{
				document.Lines.Modified -= OnDocumentLinesModified;
				document.TitlesChanged -= OnDocumentTitlesChanged;
			}
			document = await documentSource.LoadAsync();
			editHistory = document is null ? null : new(document);
			if (document is not null)
			{
				document.Lines.Modified += OnDocumentLinesModified;
				document.TitlesChanged += OnDocumentTitlesChanged;
			}
		}

		if (document is null)
		{
			document = new SheetDocument(new SheetEmptyLine());
			document.Lines.Modified += OnDocumentLinesModified;
			document.TitlesChanged += OnDocumentTitlesChanged;
		}
	}

	private void OnDocumentTitlesChanged(object? sender, EventArgs e)
		=> StateHasChanged();

	private void OnDocumentLinesModified(object? sender, ModifiedEventArgs e)
		=> StateHasChanged();

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		await base.OnAfterRenderAsync(firstRender);

		if (firstRender)
		{
			resizer = await js.InvokeAsync<IJSObjectReference>("registerResize", wrapper, DotNetObjectReference.Create(this), nameof(OnResize));
		}
	}

	private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		StateHasChanged();
	}

	[JSInvokable]
	public void OnResize(int maxCharacters)
	{
		if (this.maxCharacters == maxCharacters)
			return;

		this.maxCharacters = maxCharacters;
		StateHasChanged();
	}

	private async Task SaveDocument()
	{
		if (document is null || documentSource is null)
			return;

		await documentSource.SaveAsync(document);
	}

	private async Task OpenDroppedFile(InputFileChangeEventArgs e)
	{
		if (e.File is null)
			return;

		//Lade Datei
		var content = await fileService.OpenSelectedFileAsync(e.File);

		//Öffne Datei
		await OpenFile(content.Value);

		//Input zurücksetzen
		inputFileId = Guid.NewGuid();
	}

	private async Task OpenFile(IFileContent? content)
	{
		//Prüfe, ob Datei geöffnet werden kann
		if (content is null || !content.CanRead)
		{
			await dialogService.ShowErrorAsync("Die Datei konnte nicht geöffnet werden.", "Fehler");
			return;
		}

		//Lade Datei
		var documentSource = await documentService.LoadFile(content);
		if (!documentSource.CanLoad)
		{
			await dialogService.ShowErrorAsync("Die Datei konnte nicht gelesen werden.", "Fehler");
			return;
		}

		//Setze als aktuelles Dokument
		documentService.SetCurrentDocument(documentSource);

		//Navigiere
		var url = GetUrl(documentSource);
		var currentUri = new Uri(navigation.Uri);
		if (!new Uri(currentUri, url).Equals(currentUri))
		{
			navigation.NavigateTo(GetUrl(documentSource), new NavigationOptions()
			{
				ReplaceHistoryEntry = true,
			});
			return;
		}

		//Zeige an
		document = await documentSource.LoadAsync();
		StateHasChanged();
	}

	public async ValueTask DisposeAsync()
	{
		if (resizer is not null)
		{
			try
			{
				await resizer.InvokeVoidAsync("destroy");
				await resizer.DisposeAsync();
			}
			catch (Exception)
			{
				//Ignore
			}

			resizer = null;
		}
	}

	private enum ViewMode
	{
		Renderer,
		Editor
	}
}
