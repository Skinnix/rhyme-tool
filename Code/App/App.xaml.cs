using Skinnix.RhymeTool.MauiBlazor.Services;

namespace Skinnix.RhymeTool.MauiBlazor;

public partial class App : Application
{
	public App(IMauiUiService uiService)
	{
		InitializeComponent();

		MainPage = new MainPage(uiService);
	}
}
