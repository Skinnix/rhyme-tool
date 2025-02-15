using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Skinnix.Compoetry.Maui.Components;
using Skinnix.RhymeTool.Client.Components.Rendering;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.Compoetry.Maui.Pages.Document;

public partial class RendererPage : ContentPage, IHasShellRoute
{
	public static string Route => "/Document";

	public static Task LoadDocument(IDocumentSource document)
		=> Shell.Current.GoToAsync(Route, new Dictionary<string, object?>()
		{
			[nameof(RendererPageVM.DocumentId)] = document.Id,
		});

	protected RendererPageVM ViewModel => (RendererPageVM)BindingContext;

	public RendererPage()
	{
		BindingContext = App.Services.GetRequiredService<RendererPageVM>();

		InitializeComponent();
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);

		await ViewModel.LoadDocument(this);
	}
}

[QueryProperty("DocumentId", "DocumentId")]
public partial class RendererPageVM(IDocumentService documentService) : ViewModelBase
{
	[ObservableProperty] public partial string? DocumentId { get; set; }

	[ObservableProperty] public partial IDocumentSource? DocumentSource { get; set; }
	[ObservableProperty] public partial RootComponent? RootComponent { get; set; }
	[ObservableProperty] public partial bool IsLoading { get; set; } = true;
	[ObservableProperty] public partial RenderingSettings Settings { get; set; } = new()
	{
		FontSize = 100,
		Formatter = new DefaultSheetFormatter()
		{
			GermanMode = GermanNoteMode.Descriptive,
		}
	};

	public async Task LoadDocument(RendererPage page)
	{
		DocumentSource = await documentService.TryGetDocument(DocumentId);
		if (DocumentSource is null)
		{
			await page.DisplayAlert("Fehler", "Datei konnte nicht geladen werden", "OK");
			await Shell.Current.GoToAsync("..");
			return;
		}

		var document = await DocumentSource.LoadAsync();
		RootComponent = new()
		{
			Selector = "#app",
			ComponentType = typeof(SheetRendererWrapper),
			Parameters = new Dictionary<string, object?>()
			{
				[nameof(SheetRendererWrapper.Document)] = document,
				[nameof(SheetRendererWrapper.Settings)] = Settings,
			},
		};
		IsLoading = false;
	}
}
