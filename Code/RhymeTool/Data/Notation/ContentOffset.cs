using System.Numerics;

namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct ContentOffset(int Value)
{
	public static readonly ContentOffset Zero = new(0);
	public static readonly ContentOffset MaxValue = new(int.MaxValue);
	public static readonly ContentOffset FarEnd = MaxValue;

	public static ContentOffset operator +(ContentOffset a, ContentOffset b)
		=> new(a.Value + b.Value);

	public static ContentOffset operator -(ContentOffset a, ContentOffset b)
		=> new(a.Value - b.Value);

	public static bool operator <(ContentOffset a, ContentOffset b) => a.Value < b.Value;
	public static bool operator >(ContentOffset a, ContentOffset b) => a.Value > b.Value;
	public static bool operator <=(ContentOffset a, ContentOffset b) => a.Value <= b.Value;
	public static bool operator >=(ContentOffset a, ContentOffset b) => a.Value >= b.Value;
}
