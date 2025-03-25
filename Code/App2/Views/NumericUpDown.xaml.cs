using UraniumUI.Material.Controls;

namespace Skinnix.Compoetry.Maui.Views;

public partial class NumericUpDown : InputField
{
	public static BindableProperty ValueProperty =
		BindableProperty.Create(nameof(Value), typeof(int), typeof(NumericUpDown), 0, defaultBindingMode: BindingMode.TwoWay);

	public int Value
	{
		get => (int)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public NumericUpDown()
	{
		InitializeComponent();

#if WINDOWS
		entry.HandlerChanged += (_, _) =>
		{
			if (entry.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement entryElement)
			{
				entryElement.KeyDown += (s, e) =>
				{
					if (e.Key == Windows.System.VirtualKey.Up)
					{
						Value++;
					}
					else if (e.Key == Windows.System.VirtualKey.Down)
					{
						Value--;
					}
				};
			}
		};
#endif
	}

	public override bool HasValue => true;

	private void Increment(object sender, EventArgs e)
	{
		Value++;
	}

	private void Decrement(object sender, EventArgs e)
	{
		Value--;
	}
}