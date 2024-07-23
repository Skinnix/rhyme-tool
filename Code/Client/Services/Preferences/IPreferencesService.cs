using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.Services.Preferences;

public interface IPreferencesService
{
	bool IsPersistent { get; }

	T? GetValue<T>(string key, T? defaultValue = default)
		=> TryGetValue<T>(key, out var value) ? value : defaultValue;
	bool TryGetValue<T>(string key, out T? value);

	void SetValue<T>(string key, T value);

	void Remove(string key);
	void Clear();
}

internal class InMemoryPreferencesService : IPreferencesService
{
	private readonly Dictionary<string, object?> values = new();

	public bool IsPersistent => false;

	public bool TryGetValue<T>(string key, out T? value)
	{
		if (values.TryGetValue(key, out var objectValue))
			if (objectValue is T tValue)
			{
				value = tValue;
				return true;
			}
			else if (objectValue is null && (typeof(T).IsClass || Nullable.GetUnderlyingType(typeof(T)) is not null))
			{
				value = default;
				return true;
			}

		value = default;
		return false;
	}

	public void SetValue<T>(string key, T value)
	{
		values[key] = value;
	}

	public void Remove(string key)
		=> values.Remove(key);

	public void Clear()
		=> values.Clear();
}