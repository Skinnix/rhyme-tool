using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Services;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

internal class MauiPreferencesService : IPreferencesService
{
	public bool IsPersistent => true;

	public T? GetValue<T>(string key, T? defaultValue = default)
		=> Preferences.Default.Get(key, defaultValue);

	public bool TryGetValue<T>(string key, out T? value)
	{
		if (!Preferences.Default.ContainsKey(key))
		{
			value = default;
			return false;
		}

		value = Preferences.Default.Get(key, default(T));
		return true;
	}

	public void SetValue<T>(string key, T value)
		=> Preferences.Default.Set(key, value);

	public void Remove(string key)
		=> Preferences.Default.Remove(key);

	public void Clear()
		=> Preferences.Default.Clear();
}
