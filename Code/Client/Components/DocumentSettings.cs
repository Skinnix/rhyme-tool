using System.ComponentModel;
using System.Runtime.CompilerServices;
using Skinnix.RhymeTool.Configuration;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components;

public abstract class DocumentSettings : IConfigurable
{
	public event PropertyChangedEventHandler? PropertyChanged;

	private IReadOnlyCollection<IConfigurableProperty> properties;

	IReadOnlyCollection<IConfigurableProperty> IConfigurable.Properties => properties;

	public DocumentSettings()
	{
		properties = [.. this.GetReflectionProperties(true)];
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
		set
		{
			Formatter = Formatter with { Transformation = new SheetTransformation(value) };
			RaisePropertyChanged();
		}
	}

	[Configurable(Name = "Eindeutschen", Toggleable = true)]
	public GermanNoteMode GermanMode
	{
		get => Formatter.GermanMode;
		set
		{
			Formatter = Formatter with { GermanMode = value };
			RaisePropertyChanged();
		}
	}

	protected void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		field = value;
		RaisePropertyChanged(propertyName);
	}

	protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
