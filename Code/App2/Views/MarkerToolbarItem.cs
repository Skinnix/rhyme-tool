using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Compoetry.Maui.Views;

public class MarkerToolbarItem : ToolbarItem
{
	public FlyoutPage? FlyoutPage { get; set; }

	public MarkerToolbarItem()
	{
		IsEnabled = false;
		Text = null;
	}
}
