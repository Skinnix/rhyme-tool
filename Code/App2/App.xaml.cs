namespace Skinnix.Compoetry.Maui;

public partial class App : Application
{
	public static IServiceProvider Services => (Current as App)?.services
		?? throw new InvalidOperationException("App nicht bereit");

	private readonly IServiceProvider services;

	public App(IServiceProvider services)
	{
		this.services = services;

		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}