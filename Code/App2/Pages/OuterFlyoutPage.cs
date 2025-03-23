using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Compoetry.Maui.Pages;

public class OuterFlyoutPage : FlyoutPage
{
	public static BindableProperty ShowButtonProperty =
		BindableProperty.Create(nameof(ShowButton), typeof(bool), typeof(InnerFlyoutPage), true);

	public bool ShowButton
	{
		get => (bool)GetValue(ShowButtonProperty);
		set => SetValue(ShowButtonProperty, value);
	}

	public override bool ShouldShowToolbarButton()
		=> ShowButton && base.ShouldShowToolbarButton();
}
