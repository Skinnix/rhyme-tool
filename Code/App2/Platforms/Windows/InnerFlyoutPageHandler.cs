using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Skinnix.Compoetry.Maui.Pages;
using Skinnix.Compoetry.Maui.Views;

namespace Skinnix.Compoetry.Maui.Platforms.Windows;

internal class InnerFlyoutPageHandler : FlyoutViewHandler, IFlyoutToggleHandler
{
	/*private static readonly PropertyInfo toolbarProperty = typeof(RootNavigationView).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
		.FirstOrDefault(p => p.Name == "Toolbar" && p.PropertyType == typeof(MauiToolbar))
		?? throw new MissingMemberException();*/

	private bool visibleSet;
	private bool marginSet;
	//private Microsoft.UI.Xaml.Controls.Button? toggleButton;

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

	public override void PlatformArrange(Rect rect)
	{
		base.PlatformArrange(rect);

		UpdateFlyout();
	}

	private void UpdateFlyout()
	{
		if (!marginSet && this.VirtualView is InnerFlyoutPage customFlyout)
		{
			if (!customFlyout.ShowButton)
			{
				if (!visibleSet)
				{
					PlatformView.IsPaneToggleButtonVisible = false;
					PlatformView.AlwaysShowHeader = false;
					//visibleSet = true;
				}

				if (PlatformView.Content is Microsoft.UI.Xaml.FrameworkElement element)
				{
					element.Margin = new(0, -32, 0, 0);
					marginSet = true;
				}
			}
		}

		//PlatformView.PaneOpening += (s, e) => { var t = new StackTrace(); };

		/*if (toggleButton is null && PlatformView.KeyTipTarget is Microsoft.UI.Xaml.Controls.Button button)
		{
			toggleButton = button;
			//button.Click
		}*/
	}

	/*public void ToggleFlyout()
	{
		/*if (PlatformView.KeyTipTarget is Microsoft.UI.Xaml.Controls.Button toggleButton)
		{
			toggleButton.
			return;
		}

		IsFlyoutOpen = !IsFlyoutOpen;

		for (var current = VirtualView.Parent; current is not null; current = current.Parent)
		{
			if (current is FlyoutPage flyoutParent)
			{
				if (current.Handler?.PlatformView is RootNavigationView parentNavigationView)
				{
					var toolbar = (MauiToolbar?)toolbarProperty.GetValue(parentNavigationView);
					//PlatformView.Header = toolbar;
					//toolbarProperty.SetValue(PlatformView, toolbar);
					return;
				}
			}
		}
	}*/
}
