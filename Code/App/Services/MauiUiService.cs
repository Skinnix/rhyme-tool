using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

public interface IMauiUiService
{
	Page MainPage { get; set; }
	BlazorWebView WebView { get; set; }
}

class MauiUiService : IMauiUiService
{
	private Page? mainPage;
	private BlazorWebView? webView;

	public Page MainPage
	{
		get => mainPage ?? throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");
		set => mainPage = value;
	}

	public BlazorWebView WebView
	{
		get => webView ?? throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");
		set => webView = value;
	}
}
