using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool;

public abstract class ArrayEnumerator
{
	public static ArrayEnumerator<T> Create<T>(T[] array) => new ArrayEnumerator<T>(array);
}

public class ArrayEnumerator<T> : ArrayEnumerator, IEnumerator<T>
{
	private int index = -1;
	private readonly T[] array;

	public ArrayEnumerator(T[] array)
	{
		this.array = array;
	}

	public T Current => array[index];
	object? IEnumerator.Current => Current;

	public bool MoveNext()
	{
		index++;
		return index < array.Length;
	}

	public void Reset()
	{
		index = -1;
	}

	public void Dispose() { }
}
