using Skinnix.RhymeTool.MauiBlazor.Services;

namespace Skinnix.RhymeTool.MauiBlazor;

public partial class MainPage : ContentPage
{
	public MainPage(IMauiUiService uiService)
	{
		InitializeComponent();

		uiService.MainPage = this;
		uiService.WebView = blazorWebView;
	}
}
