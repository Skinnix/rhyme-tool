using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Skinnix.Compoetry.Maui.Views;

public class SingletonBlazorView : ContentView
{
	private readonly IMauiUiService uiService = App.Services.GetRequiredService<IMauiUiService>();

	private readonly BlazorWebView webView;

	public static readonly BindableProperty ComponentProperty = BindableProperty.Create(
		nameof(Component),
		typeof(RootComponent),
		typeof(SingletonBlazorView),
		propertyChanged: (bindable, oldValue, newValue) =>
		{
			if (bindable is not SingletonBlazorView view)
				return;

			view.SetContent(newValue as RootComponent);
		}
	);

	public RootComponent? Component
	{
		get => (RootComponent?)GetValue(ComponentProperty);
		set => SetValue(ComponentProperty, value);
	}

	public SingletonBlazorView()
	{
		if (uiService.LoadedBlazorWebView is BlazorWebView loadedWebView)
		{
			webView = loadedWebView;
		}
		else
		{
			webView = uiService.LoadedBlazorWebView = new BlazorWebView()
			{
				HostPage = "wwwroot/index.html",
			};
			webView.RootComponents.Add(new RootComponent()
			{
				ComponentType = typeof(DynamicComponentWrapper),
				Selector = "#app",
			});
		}

		TakeWebView();
	}

	protected override Size ArrangeOverride(Rect bounds)
	{
		TakeWebView();

		return base.ArrangeOverride(bounds);
	}

	protected void TakeWebView()
	{
		var parent = (SingletonBlazorView?)webView.Parent;
		if (parent == this)
			return;

		if (parent is not null)
			parent.Content = null;

		Content = webView;
		SetContent(Component);
	}

	protected void SetContent(RootComponent? component)
	{
		uiService.RootComponent = component;

		//if (component is null)
		//{
		//	webView.RootComponents.Clear();
		//	return;
		//}

		////if (webView.RootComponents.Contains(component))
		////	return;

		//webView.RootComponents.Clear();
		//webView.RootComponents.Add(component);
	}
}