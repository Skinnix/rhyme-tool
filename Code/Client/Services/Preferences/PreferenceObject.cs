using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.Services.Preferences;

//[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
//public sealed class StoredPreferenceAttribute : Attribute
//{
//	public string? Key { get; set; }

//	public StoredPreferenceAttribute(string? key = null)
//	{
//		Key = key;
//	}
//}

//public interface IPreferenceObject
//{
//	IReadOnlyCollection<KeyValuePair<string, IPropertyWrapper>> Preferences { get; }
//}

public class PreferenceObject : INotifyPropertyChanged
{
	//public static IReadOnlyCollection<KeyValuePair<string, IPropertyWrapper>> GetPreferences<T>()
	//	=> Enumerable.Empty<KeyValuePair<string, IPropertyWrapper>>().Concat(
	//		typeof(T).GetProperties()
	//		.Select(p => (Property: p, Attribute: p.GetCustomAttribute<StoredPreferenceAttribute>()))
	//		.Where(p => p.Property.CanRead && p.Property.CanWrite && p.Attribute is not null)
	//		.Select(p => new KeyValuePair<string, IPropertyWrapper>(p.Attribute?.Key ?? p.Property.Name, new ReflectionPropertyWrapper(p.Property)))
	//	).Concat(
	//		typeof(T).GetFields()
	//		.Select(f => (Field: f, Attribute: f.GetCustomAttribute<StoredPreferenceAttribute>()))
	//		.Where(f => f.Field.IsPublic && f.Attribute is not null)
	//		.Select(f => new KeyValuePair<string, IPropertyWrapper>(f.Attribute?.Key ?? f.Field.Name, new ReflectionFieldPropertyWrapper(f.Field)))
	//	).ToArray();

	public event PropertyChangedEventHandler? PropertyChanged;

	private readonly IPreferencesService service;
	private readonly string? prefix;

	public PreferenceObject(IPreferencesService service, string? prefix = null)
	{
		this.service = service;
		this.prefix = prefix;
	}

	protected T? GetValue<T>(T? defaultValue = default, [CallerMemberName] string? propertyName = null)
		=> service.GetValue(prefix + propertyName, defaultValue);

	protected T? SetValue<T>(T? value, [CallerMemberName] string? propertyName = null)
	{
		service.SetValue(prefix + propertyName, value);
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return value;
	}
}
