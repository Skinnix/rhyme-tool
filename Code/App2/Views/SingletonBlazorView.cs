using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Handlers;

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
		this.Behaviors.Add(new LifecycleBehavior());

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
	}

	public partial class LifecycleBehavior : PlatformBehavior<SingletonBlazorView, object>
	{
		protected override void OnAttachedTo(SingletonBlazorView bindable, object platformView)
		{
			base.OnAttachedTo(bindable, platformView);
		}

		protected override void OnDetachedFrom(SingletonBlazorView bindable, object platformView)
		{
			base.OnDetachedFrom(bindable, platformView);

			bindable.Content = null;
		}
	}
}
