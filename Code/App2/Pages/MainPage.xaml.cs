using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skinnix.Compoetry.Maui.IO;
using Skinnix.Compoetry.Maui.Pages.Document;
using Skinnix.RhymeTool.Client.Services;

namespace Skinnix.Compoetry.Maui.Pages;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		BindingContext = App.Services.GetRequiredService<MainPageVM>();

		InitializeComponent();
	}
}

public partial class MainPageVM(IDocumentService documentService) : ViewModelBase
{
	[RelayCommand] private async Task OpenFile()
	{
		var fileResult = await FilePicker.Default.PickAsync(new()
		{
			PickerTitle = "Songdatei öffnen",
			FileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>()
			{
				[DevicePlatform.WinUI] = [".txt", ".cbs", "cho"],
				[DevicePlatform.Android] = ["text/plain", "application/octet-stream"],
			}),
		});
		if (fileResult is null)
			return;

		var file = new PickedLocalFile(fileResult);
		var documentSource = await documentService.LoadFile(file);
		documentService.SetCurrentDocument(documentSource);

		await RendererPage.ShowDocument(documentSource);
	}
}
