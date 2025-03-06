using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.Compoetry.Maui.Pages;

public partial class SettingsPage : ContentPage
{
	public static SettingsPage Load()
		=> new(App.Services.GetRequiredService<SettingsPageVM>());

	public SettingsPage(SettingsPageVM viewModel)
	{
		BindingContext = viewModel;

		InitializeComponent();
	}
}

public partial class SettingsPageVM : ViewModelBase
{
	private readonly IDocumentFileService fileService;

	[ObservableProperty] public partial bool CanSelectWorkingDirectory { get; set; }
	[ObservableProperty] public partial bool HasWorkingDirectoryPermission { get; set; }
	[ObservableProperty] public partial string? WorkingDirectory { get; set; }

	public SettingsPageVM(IDocumentFileService fileService)
	{
		this.fileService = fileService;

		LoadSettings();
	}

	private async void LoadSettings()
	{
		if (CanSelectWorkingDirectory = fileService.CanSelectWorkingDirectory)
		{
			var workingDirectoryRequest = await fileService.TryGetWorkingDirectoryAsync();
			HasWorkingDirectoryPermission = workingDirectoryRequest.IsOk;
			WorkingDirectory = workingDirectoryRequest.Value;
		}
	}

	[RelayCommand]
	private async Task SelectWorkingDirectory()
	{
		if (!CanSelectWorkingDirectory)
			return;

		//if (!HasWorkingDirectoryPermission)
		//{
		//	var permissionRequest = await fileService.TryWorkingDirectoryPermissionAsync();
		//	if (!permissionRequest.IsOk)
		//		return;

		//	HasWorkingDirectoryPermission = true;
		//}

		var workingDirectoryRequest = await fileService.TrySelectWorkingDirectoryAsync();
		if (!workingDirectoryRequest.IsOk)
			return;

		WorkingDirectory = workingDirectoryRequest.Value;
	}
}
