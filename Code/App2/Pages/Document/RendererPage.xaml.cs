using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Skinnix.Compoetry.Maui.Components;
using Skinnix.Compoetry.Maui.Views;
using Skinnix.RhymeTool.Client.Components.Editing;
using Skinnix.RhymeTool.Client.Components.Rendering;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Data.Editing;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.Compoetry.Maui.Pages.Document;

public partial class RendererPage : InnerFlyoutPage
{
	public static Task ShowDocument(IDocumentSource document)
	{
		var page = new RendererPage();
		page.ViewModel.DocumentSource = document;
		return App.Navigation.PushAsync(page);
	}

	protected RendererPageVM ViewModel => (RendererPageVM)BindingContext;

	public RendererPage()
	{
		BindingContext = App.Services.GetRequiredService<RendererPageVM>();

		InitializeComponent();
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);

		var blankItem = new ToolbarItem();
		ToolbarItems.Add(blankItem);
		ToolbarItems.Remove(blankItem);

		await ViewModel.LoadDocument(this);
	}

	protected override bool OnBackButtonPressed()
	{
		if (Handler is IFlyoutToggleHandler toggleHandler)
		{
			if (toggleHandler.IsFlyoutOpen)
			{
				toggleHandler.IsFlyoutOpen = false;
				return true;
			}
		}
		else if (IsPresented)
		{
			IsPresented = false;
			return true;
		}

		return base.OnBackButtonPressed();
	}

	private void FlyoutButton_Clicked(object sender, EventArgs e)
	{
		if (Handler is IFlyoutToggleHandler toggleHandler)
			toggleHandler.ToggleFlyout();
		else
			IsPresented = !IsPresented;
	}
}

public partial class RendererPageVM() : ViewModelBase
{
	[ObservableProperty] public partial bool IsLoading { get; set; } = true;

	[ObservableProperty] public partial IDocumentSource? DocumentSource { get; set; }
	[ObservableProperty] public partial SheetDocument? Document { get; set; }
	[ObservableProperty] public partial string Title { get; set; } = string.Empty;
	[ObservableProperty] public partial RootComponent? RootComponent { get; set; }
	[ObservableProperty] public partial RenderingSettings RenderingSettings { get; set; } = new()
	{
		FontSize = 100,
		Formatter = new DefaultSheetFormatter()
		{
			GermanMode = GermanNoteMode.Descriptive,
		}
	};

	public async Task LoadDocument(RendererPage page)
	{
		if (Document is not null)
			return;

		if (DocumentSource is null)
		{
			await page.DisplayAlert("Fehler", "Datei konnte nicht geladen werden", "OK");
			return;
		}

		Title = string.Empty;

		Document = await DocumentSource.LoadAsync();
		Title = Document.Label ?? string.Empty;
		RootComponent = new()
		{
			ComponentType = typeof(SheetRendererWrapper),
			Parameters = new Dictionary<string, object?>()
			{
				[nameof(SheetRendererWrapper.Document)] = Document,
				[nameof(SheetRendererWrapper.Settings)] = RenderingSettings,
			},
		};
		IsLoading = false;
	}

	[RelayCommand] private Task EnterEditor()
	{
		if (IsLoading || DocumentSource is null || Document is null)
			return Task.CompletedTask;

		return EditorPage.LoadDocument(DocumentSource, Document);
	}

	[RelayCommand] private void ToggleAutofit()
	{
		RenderingSettings.Autofit = !RenderingSettings.Autofit;
	}

	[RelayCommand] private void ChangeFontSize(int delta)
	{
		RenderingSettings.FontSize += delta;
	}

	[RelayCommand] private void ChangeTranspose(int delta)
	{
		RenderingSettings.Transpose += delta;
	}
}
