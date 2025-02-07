using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Configuration;

namespace Skinnix.RhymeTool.Client.Components.Rendering;

public class RenderingSettings : DocumentSettings
{
	private bool autofit = true;
	[Configurable(Name = "Autofit")]
	public bool Autofit
	{
		get => autofit;
		set => Set(ref autofit, value);
	}
}
