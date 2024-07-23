//using System.Reflection;

//namespace Skinnix.RhymeTool.Client.Services.Preferences;

//public interface ITargetPropertyWrapper
//{
//	public object? GetValue(object target);
//	public void SetValue(object target, object? value);
//}

//public sealed class TargetDelegatePropertyWrapper : ITargetPropertyWrapper
//{
//	private readonly Func<object, object?> getter;
//	private readonly Action<object, object?> setter;

//	public object? GetValue(object target) => getter(target);
//	public void SetValue(object target, object? value) => setter(target, value);

//	public TargetDelegatePropertyWrapper(Func<object, object?> getter, Action<object, object?> setter)
//	{
//		this.getter = getter;
//		this.setter = setter;
//	}
//}

//public sealed class TargetReflectionPropertyWrapper : ITargetPropertyWrapper
//{
//	private readonly PropertyInfo property;

//	public object? GetValue(object target) => property.GetValue(target);
//	public void SetValue(object target, object? value) => property.SetValue(target, value);

//	public TargetReflectionPropertyWrapper(PropertyInfo property)
//	{
//		this.property = property;
//	}
//}

//public sealed class TargetReflectionFieldPropertyWrapper : ITargetPropertyWrapper
//{
//	private readonly FieldInfo field;

//	public object? GetValue(object target) => field.GetValue(target);
//	public void SetValue(object target, object? value) => field.SetValue(target, value);

//	public TargetReflectionFieldPropertyWrapper(FieldInfo field)
//	{
//		this.field = field;
//	}
//}
