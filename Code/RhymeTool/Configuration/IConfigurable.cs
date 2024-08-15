using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.Configuration;

public interface IConfigurable : INotifyPropertyChanged
{
	IReadOnlyCollection<IConfigurableProperty> Properties { get; }
}

public interface IConfigurableProperty
{
	string Name { get; }
	string? Category { get; }
	Type Type { get; }
	bool IsToggleable { get; set; }
	object? Value { get; set; }
	int Step { get; }
}

public static class ConfigurableExtensions
{
	public static IEnumerable<IConfigurableProperty> GetReflectionProperties(this IConfigurable configurable, bool needsAttribute)
	{
		foreach (var property in configurable.GetType().GetProperties())
		{
			if (property.GetGetMethod()?.IsPublic != true || property.GetSetMethod()?.IsPublic != true)
				continue;

			var attribute = property.GetCustomAttribute<ConfigurableAttribute>(false);
			if (needsAttribute && attribute is null)
				continue;

			var reflectionProperty = ReflectionProperty.Create(attribute?.Name ?? property.Name, property, configurable);
			if (attribute is not null)
			{
				reflectionProperty.Category = attribute.Category;
				reflectionProperty.IsToggleable = attribute.Toggleable;
				reflectionProperty.Step = attribute.Step;
			}

			yield return reflectionProperty;
		}
	}

	private abstract class ReflectionProperty(string name, Type type) : IConfigurableProperty
	{
		public string Name { get; set; } = name;
		public Type Type { get; } = type;

		public string? Category { get; set; }
		public bool IsToggleable { get; set; }
		public int Step { get; set; }

		public abstract object? Value { get; set; }

		public static ReflectionProperty Create(string name, PropertyInfo property, object target)
			=> (ReflectionProperty)createMethod.MakeGenericMethod(property.PropertyType).Invoke(null, [name, property, target])!;

		private static readonly MethodInfo createMethod = typeof(ReflectionProperty).GetMethod(nameof(CreateGeneric), [typeof(string), typeof(PropertyInfo), typeof(object)])
			?? throw new MissingMethodException();
		public static ReflectionProperty CreateGeneric<T>(string name, PropertyInfo property, object target)
			=> new ReflectionProperty<T>(name, property.PropertyType,
				property.GetGetMethod()!.CreateDelegate<Func<T?>>(target),
				property.GetSetMethod()!.CreateDelegate<Action<T?>>(target));
	}

	private sealed class ReflectionProperty<T>(string name, Type type, Func<T?> getValue, Action<T?> setValue)
		: ReflectionProperty(name, type)
	{
		public override object? Value
		{
			get => getValue();
			set => setValue((T?)value);
		}
	}
}
