using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Skinnix.Compoetry.Maui.Pages;
using Skinnix.Compoetry.Maui.Views;

namespace Skinnix.Compoetry.Maui.Platforms.Windows;

internal class InnerFlyoutPageHandler : FlyoutViewHandler, IFlyoutToggleHandler
{
	public bool IsFlyoutOpen
	{
		get => PlatformView.IsPaneOpen;
		set => PlatformView.IsPaneOpen = value;
	}

	protected override void ConnectHandler(RootNavigationView platformView)
	{
		base.ConnectHandler(platformView);

		UpdateFlyout();
	}

	public override void UpdateValue(string property)
	{
		base.UpdateValue(property);

		UpdateFlyout();
	}

	private void UpdateFlyout()
	{
		if (this.VirtualView is InnerFlyoutPage customFlyout)
		{
			if (!customFlyout.ShowButton)
			{
				PlatformView.IsPaneToggleButtonVisible = false;
				PlatformView.AlwaysShowHeader = false;

				if (PlatformView.Content is Microsoft.UI.Xaml.FrameworkElement element)
					element.Margin = new(0, -32, 0, 0);
			}
		}
	}
}
