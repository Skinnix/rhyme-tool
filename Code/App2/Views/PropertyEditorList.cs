using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using UraniumUI;
using UraniumUI.Options;

namespace Skinnix.Compoetry.Maui.Views;

public class PropertyEditorList : VerticalStackLayout
{
	public static BindableProperty SourceProperty =
		BindableProperty.Create(nameof(Source), typeof(object), typeof(PropertyEditorList), null, propertyChanged: OnSourceChanged);

	public object? Source
	{
		get => GetValue(SourceProperty);
		set => SetValue(SourceProperty, value);
	}

	private readonly Dictionary<Type, AutoFormViewOptions.EditorForType> editorMapping;
	private readonly Func<System.Reflection.PropertyInfo, string> propertyNameFactory;

	public PropertyEditorList()
	{
		Spacing = 20;
        Padding = 10;

		var options = UraniumServiceProvider.Current.GetRequiredService<IOptions<AutoFormViewOptions>>().Value;
		editorMapping = options.EditorMapping;
		propertyNameFactory = options.PropertyNameFactory;
	}

	private static void OnSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
	{
		if (bindable is not PropertyEditorList propertyEditorList)
			return;

		propertyEditorList.OnSourceChanged(newValue);
	}

	private void OnSourceChanged(object? source)
	{
		Children.Clear();
		if (source is null)
			return;

		var properties = source.GetType().GetProperties();
		using (this.Batch())
		{
			foreach (var property in properties)
			{
				if (!editorMapping.TryGetValue(property.PropertyType, out var editorForType))
					continue;

				var editor = editorForType(property, propertyNameFactory, source);
				if (editor is not null)
					Children.Add(editor);
			}
		}
	}
}
