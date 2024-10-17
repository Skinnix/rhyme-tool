using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct TabNote
{
	public static readonly TabNote Empty = default;

	private readonly int value;

	public int? Value => value == 0 ? null : value - 1;

	[MemberNotNullWhen(false, nameof(Value))]
	public bool IsEmpty => value == 0;

	public TabNote(int? value)
	{
		if (value < 0)
			throw new ArgumentOutOfRangeException(nameof(value), "Note kann nicht kleiner als Null sein");

		this.value = value.GetValueOrDefault(-1) + 1;
	}

	public override string ToString()
		=> Value is null ? "-"
		: Value.Value.ToString();
}