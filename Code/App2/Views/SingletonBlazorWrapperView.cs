using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Maui.Handlers;

namespace Skinnix.Compoetry.Maui.Views;

public class SingletonWrapperBlazorView : ContentView
{
	private readonly bool useSingleton;

	private readonly IMauiUiService uiService = App.Services.GetRequiredService<IMauiUiService>();

	private readonly Grid grid;
	private readonly BlazorWebView webView;

	public static readonly BindableProperty ComponentProperty = BindableProperty.Create(
		nameof(Component),
		typeof(RootComponent),
		typeof(SingletonWrapperBlazorView),
		propertyChanged: (bindable, oldValue, newValue) =>
		{
			if (bindable is not SingletonWrapperBlazorView view)
				return;

			view.SetContent(newValue as RootComponent);
		}
	);

	public RootComponent? Component
	{
		get => (RootComponent?)GetValue(ComponentProperty);
		set => SetValue(ComponentProperty, value);
	}

	public SingletonWrapperBlazorView() : this(true) { }

	public SingletonWrapperBlazorView(bool useSingleton)
	{
		this.useSingleton = useSingleton;

		if (useSingleton)
		{
			this.Behaviors.Add(new LifecycleBehavior());

			if (uiService.LoadedBlazorWebViewGrid is Grid loadedGrid)
			{
				grid = loadedGrid;
				webView = uiService.LoadedBlazorWebView!;
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
				
				grid = uiService.LoadedBlazorWebViewGrid = new Grid();
				grid.Children.Add(webView);
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

			grid = uiService.LoadedBlazorWebViewGrid = new Grid();
			grid.Children.Add(webView);
		}

		TakeGrid();
	}

	protected override Size ArrangeOverride(Rect bounds)
	{
		if (useSingleton)
			TakeGrid();

		return base.ArrangeOverride(bounds);
	}

	protected void TakeGrid()
	{
		var parent = (SingletonWrapperBlazorView?)grid.Parent;
		if (parent == this)
			return;

		if (parent is not null)
			parent.Content = null;

		Content = grid;
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
				 if (webView.RootComponents[0].Equals(component))
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

	public partial class LifecycleBehavior : PlatformBehavior<SingletonWrapperBlazorView, object>
	{
		protected override void OnAttachedTo(SingletonWrapperBlazorView bindable, object platformView)
		{
			base.OnAttachedTo(bindable, platformView);
		}

		protected override void OnDetachedFrom(SingletonWrapperBlazorView bindable, object platformView)
		{
			base.OnDetachedFrom(bindable, platformView);

			//bindable.Content = null;
		}
	}
}
