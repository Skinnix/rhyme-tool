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

public partial class EditorPage : InnerFlyoutPage
{
	public static Task LoadDocument(IDocumentSource documentSource, SheetDocument document)
	{
		var page = new EditorPage();
		page.ViewModel.SetDocument(documentSource, document);
		return App.Navigation.PushAsync(page);
	}

	protected EditorPageVM ViewModel => (EditorPageVM)BindingContext;

	public EditorPage()
	{
		BindingContext = App.Services.GetRequiredService<EditorPageVM>();

		InitializeComponent();
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);

		var blankItem = new ToolbarItem();
		ToolbarItems.Add(blankItem);
		ToolbarItems.Remove(blankItem);
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

public partial class EditorPageVM() : ViewModelBase
{
	[ObservableProperty] public partial IDocumentSource? DocumentSource { get; private set; }
	[ObservableProperty] public partial SheetDocument? Document { get; private set; }

	[ObservableProperty] public partial DocumentEditHistory? EditHistory { get; set; }
	[ObservableProperty] public partial RootComponent? RootComponent { get; set; }
	[ObservableProperty] public partial EditingSettings EditingSettings { get; set; } = new()
	{
		FontSize = 100,
		Formatter = new DefaultSheetFormatter()
		{
			GermanMode = GermanNoteMode.Descriptive,
		}
	};

	public void SetDocument(IDocumentSource documentSource, SheetDocument document)
	{
		DocumentSource = documentSource;
		Document = document;
		EditHistory = new(Document);
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
	}
}
