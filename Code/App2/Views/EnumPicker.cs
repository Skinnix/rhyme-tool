using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data;
using UraniumUI.Material.Controls;

namespace Skinnix.Compoetry.Maui.Views;

public class EnumPicker<TEnum> : ContentView
{
	public static BindableProperty TitleProperty =
		BindableProperty.Create(nameof(Title), typeof(string), typeof(EnumPicker<TEnum>), string.Empty, propertyChanged: OnTitleChanged);

	public string Title
	{
		get => (string)GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	private static void OnTitleChanged(BindableObject bindable, object oldValue, object newValue)
	{
		if (bindable is not EnumPicker<TEnum> picker)
			return;

		picker.picker.Title = (string?)newValue;
	}

	public static BindableProperty ValueProperty =
		BindableProperty.Create(nameof(Value), typeof(TEnum), typeof(EnumPicker<TEnum>), default(TEnum), defaultBindingMode: BindingMode.TwoWay);

	public TEnum Value
	{
		get => (TEnum)GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}

	private readonly PickerField picker = new();

	public EnumPicker()
	{
		var enumType = typeof(TEnum);
		picker.AllowClear = false;
		if (!enumType.IsEnum)
		{
			enumType = Nullable.GetUnderlyingType(enumType)
				?? throw new InvalidOperationException("Type must be an enum or nullable enum.");
			picker.AllowClear = true;
		}

		var rawValues = Enum.GetValues(enumType);
		var values = new Entry[rawValues.Length];
		for (var i = 0; i < rawValues.Length; i++)
		{
			var value = rawValues.GetValue(i);
			var name = value is null ? string.Empty : EnumNameAttribute.GetDisplayName(enumType, value);
			values[i] = new(value, name);
		}

		picker.ItemsSource = values;
		picker.SetBinding(PickerField.SelectedItemProperty, new Binding(nameof(Value), BindingMode.TwoWay, converter: EnumConverter.Instance, source: this));
		Content = picker;
	}

	private sealed class Entry(object? value, string name)
	{
		public object? Value { get; } = value;

		public override string ToString() => name;

		public override bool Equals(object? obj)
			=> obj is Entry entry ? Equals(entry.Value, Value)
			: Equals(obj, Value);

		public override int GetHashCode()
			=> Value?.GetHashCode() ?? 0;
	}

	private class EnumConverter : IValueConverter
	{
		public static readonly EnumConverter Instance = new EnumConverter();

		object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Convert(value);
		public static Entry Convert(object? value)
		{
			var name = value is null ? string.Empty : EnumNameAttribute.GetDisplayName(value.GetType(), value);
			return new Entry(value, name);
		}

		object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
			=> value is Entry entry ? ConvertBack(entry) : null;
		public static object? ConvertBack(Entry entry)
			=> entry.Value;
	}
}
