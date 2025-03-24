using Skinnix.Compoetry.Maui.Pages;

namespace Skinnix.Compoetry.Maui;

public partial class App : Application
{
	public static IServiceProvider Services => (Current as App)?.services
		?? throw new InvalidOperationException("App nicht bereit");

	private static INavigation? navigation;
	public static INavigation Navigation
	{
		get => navigation ?? throw new InvalidOperationException("App nicht bereit");
		set => navigation = value;
	}

	private readonly IServiceProvider services;

	public App(IServiceProvider services)
	{
		this.services = services;

		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		/*var window = new Window(new FlyoutPageWrapper(new MainPage()))
		{
			//TitleBar = new TitleBar()
			//{
			//	Content = new Button()
			//	{
			//		Text = "Title Bar"
			//	}
			//}
		};
		return window;*/
		return new AppWindow();
	}
}
