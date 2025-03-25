using UraniumUI.Material.Controls;

namespace Skinnix.Compoetry.Maui.Views;

public partial class NumericUpDown1 : ContentView
{
	public static BindableProperty ValueProperty =
		BindableProperty.Create(nameof(Value), typeof(int), typeof(NumericUpDown), 0, defaultBindingMode: BindingMode.TwoWay);

	public int Value
	{
		get => (int)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	public static BindableProperty TitleProperty =
		BindableProperty.Create(nameof(Title), typeof(string), typeof(NumericUpDown), string.Empty);

	public string? Title
	{
		get => (string?)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public NumericUpDown1()
	{
		InitializeComponent();
	}

	private void Increment(object sender, EventArgs e)
	{
		Value++;
    }

	private void Decrement(object sender, EventArgs e)
	{
		Value--;
    }
}