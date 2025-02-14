using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Skinnix.Compoetry.Maui;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		BindingContext = App.Services.GetRequiredService<MainPageVM>();

		InitializeComponent();
	}
}

public partial class MainPageVM : ViewModelBase
{
	[RelayCommand]
	private Task NavigateToTestPage()
		=> Shell.Current.GoToAsync("TestPage");
}
