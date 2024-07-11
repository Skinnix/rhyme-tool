namespace Skinnix.RhymeTool.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigurableAttribute : Attribute
{
	public string? Name { get; set; }
	public string? Category { get; set; }
	public bool Toggleable { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
public class MinValueAttribute : Attribute
{
	public object? Value { get; }

	public MinValueAttribute(object? value)
	{
		Value = value;
	}
}

[AttributeUsage(AttributeTargets.Property)]
public class MaxValueAttribute : Attribute
{
	public object? Value { get; }

	public MaxValueAttribute(object? value)
	{
		Value = value;
	}
}
