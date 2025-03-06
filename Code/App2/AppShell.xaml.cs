using Skinnix.Compoetry.Maui.Pages;

namespace Skinnix.Compoetry.Maui;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
	}

	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);

		if (args.Current.Location.ToString() == "/")
		{
			CurrentItem = defaultContent;
		}
	}

	private async void Settings_Clicked(object sender, EventArgs e)
	{
		await Shell.Current.Navigation.PushAsync(SettingsPage.Load());
    }
}
