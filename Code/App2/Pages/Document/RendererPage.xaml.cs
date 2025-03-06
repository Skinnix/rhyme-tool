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

public partial class DocumentPage : InnerFlyoutPage
{
	public static Task LoadDocument(IDocumentSource document)
	{
		var page = new DocumentPage();
		page.ViewModel.DocumentSource = document;
		return App.Navigation.PushAsync(page);
	}

	protected DocumentPageVM ViewModel => (DocumentPageVM)BindingContext;

	public DocumentPage()
	{
		BindingContext = App.Services.GetRequiredService<DocumentPageVM>();

		InitializeComponent();

		UpdateToolbarItems();
		ViewModel.PropertyChanged += ViewModel_PropertyChanged;
	}

	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);

		await ViewModel.LoadDocument(this);
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(DocumentPageVM.IsEditing):
				UpdateToolbarItems();
				break;
		}
	}

	private void UpdateToolbarItems()
	{
		if (ViewModel.IsEditing)
		{
			ExcludeToolbarItems(enterEditorToolbarItem);
			IncludeToolbarItems(undoToolbarItem, redoToolbarItem, enterViewerToolbarItem);
		}
		else
		{
			ExcludeToolbarItems(undoToolbarItem, redoToolbarItem, enterViewerToolbarItem);
			IncludeToolbarItems(enterEditorToolbarItem);
		}
	}

	private void IncludeToolbarItems(params ToolbarItem[] items)
	{
		foreach (var item in items)
			IncludeToolbarItem(item);
	}
	private void IncludeToolbarItem(ToolbarItem item)
	{
		if (ToolbarItems.Contains(item))
			return;

		var last = ToolbarItems.Index().LastOrDefault(i => i.Item.Priority > item.Priority);
		if (last.Item is null)
			ToolbarItems.Add(item);
		else
			ToolbarItems.Insert(last.Index, item);
	}

	private void ExcludeToolbarItems(params ToolbarItem[] items)
	{
		foreach (var item in items)
			ExcludeToolbarItem(item);
	}
	private void ExcludeToolbarItem(ToolbarItem item)
	{
		ToolbarItems.Remove(item);
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

public partial class DocumentPageVM() : ViewModelBase
{
	[ObservableProperty] public partial bool IsLoading { get; set; } = true;
	[ObservableProperty] public partial bool IsEditing { get; set; } = false;

	[ObservableProperty] public partial IDocumentSource? DocumentSource { get; set; }
	[ObservableProperty] public partial SheetDocument? Document { get; set; }
	[ObservableProperty] public partial DocumentEditHistory? EditHistory { get; set; }
	[ObservableProperty] public partial RootComponent? RootComponent { get; set; }
	[ObservableProperty] public partial RenderingSettings RenderingSettings { get; set; } = new()
	{
		FontSize = 100,
		Formatter = new DefaultSheetFormatter()
		{
			GermanMode = GermanNoteMode.Descriptive,
		}
	};
	[ObservableProperty] public partial EditingSettings EditingSettings { get; set; } = new()
	{
		FontSize = 100,
		Formatter = new DefaultSheetFormatter()
		{
			GermanMode = GermanNoteMode.Descriptive,
		}
	};

	public async Task LoadDocument(DocumentPage page)
	{
		if (DocumentSource is null)
		{
			await page.DisplayAlert("Fehler", "Datei konnte nicht geladen werden", "OK");
			//await App.Navigation.PopAsync();
			return;
		}

		Document = await DocumentSource.LoadAsync();
		EditHistory = new(Document);
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
		IsEditing = false;
	}

	[RelayCommand] private void EnterEditor()
	{
		if (IsLoading || IsEditing)
			return;

		RootComponent = new()
		{
			ComponentType = typeof(SheetEditorWrapper),
			Parameters = new Dictionary<string, object?>()
			{
				[nameof(SheetEditorWrapper.Document)] = Document,
				[nameof(SheetEditorWrapper.Settings)] = EditingSettings,
				[nameof(SheetEditorWrapper.EditHistory)] = EditHistory,
			},
		};
		IsEditing = true;
	}

	[RelayCommand] private void EnterViewer()
	{
		if (IsLoading || !IsEditing)
			return;

		RootComponent = new()
		{
			ComponentType = typeof(SheetRendererWrapper),
			Parameters = new Dictionary<string, object?>()
			{
				[nameof(SheetRendererWrapper.Document)] = Document,
				[nameof(SheetRendererWrapper.Settings)] = RenderingSettings,
			},
		};
		IsEditing = false;
	}
}
