using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Handlers;

namespace Skinnix.Compoetry.Maui.Views;

public class SingletonBlazorView : ContentView
{
	private readonly bool useSingleton;

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

	public SingletonBlazorView() : this(true) { }

	public SingletonBlazorView(bool useSingleton)
	{
		this.useSingleton = useSingleton;

		if (useSingleton)
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
		}
		else
		{
			webView = uiService.LoadedBlazorWebView = new BlazorWebView()
			{
				HostPage = "wwwroot/index.html",
			};
			if (Component is not null)
			{
				var useComponent = Component;
				if (string.IsNullOrEmpty(useComponent.Selector))
					useComponent = new RootComponent()
					{
						ComponentType = useComponent.ComponentType,
						Parameters = useComponent.Parameters,
						Selector = "#app",
					};

				webView.RootComponents.Add(useComponent);
			}
		}

		TakeWebView();
	}

	protected override Size ArrangeOverride(Rect bounds)
	{
		if (useSingleton)
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
		if (useSingleton)
		{
			uiService.RootComponent = component;
		}
		else
		{
			while (webView.RootComponents.Count != 0)
			{
				 if (webView.RootComponents[0].ComponentType == component?.ComponentType
					&& webView.RootComponents[0].Parameters == component?.Parameters)
					return;

				webView.RootComponents.RemoveAt(0);
			}

			if (component is not null)
			{
				var useComponent = component;
				if (string.IsNullOrEmpty(useComponent.Selector))
					useComponent = new RootComponent()
					{
						ComponentType = useComponent.ComponentType,
						Parameters = useComponent.Parameters,
						Selector = "#app",
					};

				webView.RootComponents.Add(useComponent);
			}
		}
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

			//bindable.Content = null;
		}
	}
}
