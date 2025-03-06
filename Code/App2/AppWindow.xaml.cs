using CommunityToolkit.Mvvm.Input;
using Skinnix.Compoetry.Maui.Pages;

namespace Skinnix.Compoetry.Maui;

public partial class AppWindow : Window
{
	public static AppWindow Current => App.Current?.Windows.FirstOrDefault() as AppWindow
		?? throw new InvalidOperationException("App nicht bereit");

	//public new INavigation Navigation => navigationPage.Navigation;

	public AppWindow()
	{
		BindingContext = App.Services.GetRequiredService<AppWindowVM>();
		
		InitializeComponent();

		App.Navigation = navigationPage.Navigation;
	}

	private void navigationPage_Pushed(object sender, NavigationEventArgs e)
	{
		flyoutPage.IsGestureEnabled = false;
		flyoutPage.IsPresented = false;
	}

	private void navigationPage_PoppedToRoot(object sender, NavigationEventArgs e)
	{
		flyoutPage.IsGestureEnabled = true;
	}

	private void navigationPage_Popped(object sender, NavigationEventArgs e)
	{
		if (navigationPage.Navigation.NavigationStack.Count == 1 && navigationPage.Navigation.NavigationStack[0] is MainPage)
			flyoutPage.IsGestureEnabled = true;
	}
}

public partial class AppWindowVM : ViewModelBase
{
	public AppWindowVM()
	{
		//MainThread.InvokeOnMainThreadAsync(async () =>
		//{
		//	await Task.Delay(5000);
		//	OpenSettings();
		//});
	}

	[RelayCommand] private async void OpenSettings()
	{
		await App.Navigation.PushAsync(SettingsPage.Load());
	}
}
