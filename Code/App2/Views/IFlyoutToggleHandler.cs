using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Compoetry.Maui.Views;

public interface IFlyoutToggleHandler
{
	bool IsFlyoutOpen { get; set; }

	void ToggleFlyout()
	{
		IsFlyoutOpen = !IsFlyoutOpen;
	}
}
