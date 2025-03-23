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

internal class OuterFlyoutPageHandler : FlyoutViewHandler, IFlyoutToggleHandler
{
	public static new IPropertyMapper<IFlyoutView, IFlyoutViewHandler> Mapper = new PropertyMapper<IFlyoutView, OuterFlyoutPageHandler>(FlyoutViewHandler.Mapper)
	{
		[nameof(OuterFlyoutPage.ShowButton)] = ShowButton,
	};

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

	private static void ShowButton(OuterFlyoutPageHandler handler, IFlyoutView flyoutView)
	{
		handler.UpdateValue(nameof(OuterFlyoutPage.ShowButton));
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
		if (this.VirtualView is OuterFlyoutPage customFlyout)
		{
			PlatformView.IsPaneToggleButtonVisible = customFlyout.ShowButton;
		}
	}
}
