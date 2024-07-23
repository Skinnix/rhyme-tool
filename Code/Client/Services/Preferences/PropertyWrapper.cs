//using System.Reflection;

//namespace Skinnix.RhymeTool.Client.Services.Preferences;

//public interface IPropertyWrapper
//{
//	public object? GetValue();
//	public void SetValue(object? value);
//}

//public sealed class DelegatePropertyWrapper : IPropertyWrapper
//{
//	private readonly Func< object?> getter;
//	private readonly Action< object?> setter;

//	public object? GetValue() => getter();
//	public void SetValue(object? value) => setter(value);

//	public DelegatePropertyWrapper(Func<object?> getter, Action<object?> setter)
//	{
//		this.getter = getter;
//		this.setter = setter;
//	}
//}

//public sealed class ReflectionPropertyWrapper : IPropertyWrapper
//{
//	private readonly object instance;
//	private readonly PropertyInfo property;

//	public object? GetValue() => property.GetValue(instance);
//	public void SetValue(object? value) => property.SetValue(instance, value);

//	public ReflectionPropertyWrapper(object instance, PropertyInfo property)
//	{
//		this.instance = instance;
//		this.property = property;
//	}
//}

//public sealed class ReflectionFieldPropertyWrapper : IPropertyWrapper
//{
//	private readonly object instance;
//	private readonly FieldInfo field;

//	public object? GetValue() => field.GetValue(instance);
//	public void SetValue(object? value) => field.SetValue(instance, value);

//	public ReflectionFieldPropertyWrapper(object instance, FieldInfo field)
//	{
//		this.instance = instance;
//		this.field = field;
//	}
//}
