using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data;

public interface IEither
{
	public object? Value { get; }
}

public readonly struct Either<T1, T2> : IEither
	where T1 : class
	where T2 : class
{
	public object? Value { get; }

	public Either(T1 value) => Value = value;
	public Either(T2 value) => Value = value;

	public bool Is<T>() => Value is T;

	public bool Is<T>([MaybeNullWhen(false)] out T value)
	{
		if (Value is T t)
		{
			value = t;
			return true;
		}

		value = default;
		return false;
	}

	public void Whether(Action<T1> f1, Action<T2> f2)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			default:
				throw new InvalidOperationException();
		}
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action? @default)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			default:
				@default?.Invoke();
				break;
		}
	}

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		_ => throw new InvalidOperationException(),
	};

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<TResult> @default) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		_ => @default(),
	};

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	public override bool Equals(object? obj) => obj is IEither either && Equals(Value, either.Value);
	public override string ToString() => Value?.ToString() ?? string.Empty;

	public static implicit operator Either<T1, T2>(T1 value) => new(value);
	public static implicit operator Either<T1, T2>(T2 value) => new(value);
}

public readonly struct Either<T1, T2, T3> : IEither
	where T1 : class
	where T2 : class
	where T3 : class
{
	public object? Value { get; }

	public Either(T1 value) => Value = value;
	public Either(T2 value) => Value = value;
	public Either(T3 value) => Value = value;

	public bool Is<T>() => Value is T;

	public bool Is<T>([MaybeNullWhen(false)] out T value)
	{
		if (Value is T t)
		{
			value = t;
			return true;
		}

		value = default;
		return false;
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action<T3> f3)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			case T3 t3:
				f3(t3);
				break;
			default:
				throw new InvalidOperationException();
		}
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action<T3> f3, Action? @default)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			case T3 t3:
				f3(t3);
				break;
			default:
				@default?.Invoke();
				break;
		}
	}

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		T3 t3 => f3(t3),
		_ => throw new InvalidOperationException(),
	};

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3, Func<TResult> @default) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		T3 t3 => f3(t3),
		_ => @default(),
	};

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	public override bool Equals(object? obj) => obj is IEither either && Equals(Value, either.Value);
	public override string ToString() => Value?.ToString() ?? string.Empty;

	public static implicit operator Either<T1, T2, T3>(T1 value) => new(value);
	public static implicit operator Either<T1, T2, T3>(T2 value) => new(value);
	public static implicit operator Either<T1, T2, T3>(T3 value) => new(value);
}

public readonly struct Either<T1, T2, T3, T4> : IEither
	where T1 : class
	where T2 : class
	where T3 : class
	where T4 : class
{
	public object? Value { get; }

	public Either(T1 value) => Value = value;
	public Either(T2 value) => Value = value;
	public Either(T3 value) => Value = value;
	public Either(T4 value) => Value = value;

	public bool Is<T>() => Value is T;

	public bool Is<T>([MaybeNullWhen(false)] out T value)
	{
		if (Value is T t)
		{
			value = t;
			return true;
		}

		value = default;
		return false;
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			case T3 t3:
				f3(t3);
				break;
			case T4 t4:
				f4(t4);
				break;
			default:
				throw new InvalidOperationException();
		}
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4, Action? @default)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			case T3 t3:
				f3(t3);
				break;
			case T4 t4:
				f4(t4);
				break;
			default:
				@default?.Invoke();
				break;
		}
	}

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3, Func<T4, TResult> f4) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		T3 t3 => f3(t3),
		T4 t4 => f4(t4),
		_ => throw new InvalidOperationException(),
	};

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3, Func<T4, TResult> f4, Func<TResult> @default) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		T3 t3 => f3(t3),
		T4 t4 => f4(t4),
		_ => @default(),
	};

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	public override bool Equals(object? obj) => obj is IEither either && Equals(Value, either.Value);
	public override string ToString() => Value?.ToString() ?? string.Empty;

	public static implicit operator Either<T1, T2, T3, T4>(T1 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4>(T2 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4>(T3 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4>(T4 value) => new(value);
}

public readonly struct Either<T1, T2, T3, T4, T5> : IEither
	where T1 : class
	where T2 : class
	where T3 : class
	where T4 : class
	where T5 : class
{
	public object? Value { get; }

	public Either(T1 value) => Value = value;
	public Either(T2 value) => Value = value;
	public Either(T3 value) => Value = value;
	public Either(T4 value) => Value = value;
	public Either(T5 value) => Value = value;

	public bool Is<T>() => Value is T;

	public bool Is<T>([MaybeNullWhen(false)] out T value)
	{
		if (Value is T t)
		{
			value = t;
			return true;
		}

		value = default;
		return false;
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4, Action<T5> f5)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			case T3 t3:
				f3(t3);
				break;
			case T4 t4:
				f4(t4);
				break;
			case T5 t5:
				f5(t5);
				break;
			default:
				throw new InvalidOperationException();
		}
	}

	public void Whether(Action<T1> f1, Action<T2> f2, Action<T3> f3, Action<T4> f4, Action<T5> f5, Action? @default)
	{
		switch (Value)
		{
			case T1 t1:
				f1(t1);
				break;
			case T2 t2:
				f2(t2);
				break;
			case T3 t3:
				f3(t3);
				break;
			case T4 t4:
				f4(t4);
				break;
			case T5 t5:
				f5(t5);
				break;
			default:
				@default?.Invoke();
				break;
		}
	}

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3, Func<T4, TResult> f4, Func<T5, TResult> f5) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		T3 t3 => f3(t3),
		T4 t4 => f4(t4),
		T5 t5 => f5(t5),
		_ => throw new InvalidOperationException(),
	};

	public TResult Switch<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3, Func<T4, TResult> f4, Func<T5, TResult> f5, Func<TResult> @default) => Value switch
	{
		T1 t1 => f1(t1),
		T2 t2 => f2(t2),
		T3 t3 => f3(t3),
		T4 t4 => f4(t4),
		T5 t5 => f5(t5),
		_ => @default(),
	};

	public override int GetHashCode() => Value?.GetHashCode() ?? 0;
	public override bool Equals(object? obj) => obj is IEither either && Equals(Value, either.Value);
	public override string ToString() => Value?.ToString() ?? string.Empty;

	public static implicit operator Either<T1, T2, T3, T4, T5>(T1 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4, T5>(T2 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4, T5>(T3 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4, T5>(T4 value) => new(value);
	public static implicit operator Either<T1, T2, T3, T4, T5>(T5 value) => new(value);
}
