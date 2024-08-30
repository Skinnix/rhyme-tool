using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public class EnumNameAttribute : Attribute
{
	private static ConcurrentDictionary<Type, CharacterTree<object>> parseCache = new();

	public string Name { get; }
	public string[] AlternativeNames { get; }
	public string[] Blacklist { get; set; } = Array.Empty<string>();

	public EnumNameAttribute(string name, params string[] alternativeNames)
	{
		Name = name;
		AlternativeNames = alternativeNames;
	}

	public static string GetDisplayName<T>(T value)
		where T : struct, Enum
	{
		var type = typeof(T);
		var name = value.ToString();
		var field = type.GetField(name);
		if (field == null)
			throw new ArgumentException($"Enum {type.Name} has no field {name}");

		var attribute = field.GetCustomAttribute<EnumNameAttribute>(false);
		return attribute?.Name ?? name;
	}

	public static bool TryParse<T>(string s, out T value)
		where T : struct, Enum
	{
		var type = typeof(T);
		var cache = parseCache.AddOrUpdate(type, t =>
		{
			var cache = new CharacterTree<object>();
			foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				var attribute = field.GetCustomAttribute<EnumNameAttribute>(false);
				if (attribute == null)
					continue;

				cache.Set(field.Name, field.GetValue(null)!);
				foreach (var alternativeName in attribute.AlternativeNames)
					cache.Set(alternativeName, field.GetValue(null)!);
			}
			return cache;
		}, (_, cache) => cache);

		if (cache.TryGetValue(s, out var objValue))
		{
			value = (T)objValue;
			return true;
		}

		value = default;
		return false;
	}

	public static int TryRead<T>(ReadOnlySpan<char> s, out T value)
		where T : struct, Enum
	{
		var type = typeof(T);
		var cache = parseCache.AddOrUpdate(type, t =>
		{
			var cache = new CharacterTree<object>();
			foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				var attribute = field.GetCustomAttribute<EnumNameAttribute>(false);
				if (attribute == null)
					continue;

				var fieldValue = field.GetValue(null)!;
				cache.Set(attribute.Name, fieldValue, attribute.Blacklist);
				foreach (var alternativeName in attribute.AlternativeNames)
					cache.Set(alternativeName, fieldValue, attribute.Blacklist);
			}
			return cache;
		}, (_, cache) => cache);

		var length = cache.TryRead(s, out var objValue);
		if (length == -1)
		{
			value = default;
			return -1;
		}

		value = (T)objValue!;
		return length;
	}
}
