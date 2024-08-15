using System.ComponentModel;
using System.Runtime.CompilerServices;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Configuration;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components.Rendering;

public class EditingSettings : IConfigurable
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private IReadOnlyCollection<IConfigurableProperty> properties;

	IReadOnlyCollection<IConfigurableProperty> IConfigurable.Properties => properties;

	public EditingSettings()
	{
		properties = [..this.GetReflectionProperties(true)];
	}

	private DefaultSheetFormatter formatter = new();
	public DefaultSheetFormatter Formatter
	{
		get => formatter;
		set => Set(ref formatter, value);
	}

	private int fontSize = 100;
	[Configurable(Name = "Schriftgröße", Toggleable = true, Step = 5)]
	public int FontSize
	{
		get => fontSize;
		set => Set(ref fontSize, value);
	}

	[Configurable(Name = "Transponieren", Toggleable = true)]
	public int Transpose
	{
		get => Formatter.Transformation?.Transpose ?? 0;
		set => Formatter = Formatter with
		{
			Transformation = new SheetTransformation(value)
		};
	}

	[Configurable(Name = "Leere Akkordzeilen anzeigen")]
	public bool ShowEmptyChordLines
	{
		get => Formatter.ShowEmptyAttachmentLines;
		set => Formatter = Formatter with
		{
			ShowEmptyAttachmentLines = value
		};
	}

	private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
