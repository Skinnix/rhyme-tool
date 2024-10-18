using System.Diagnostics.CodeAnalysis;

namespace Skinnix.RhymeTool.Data.Notation;

[Flags]
public enum TabNoteModifier : short
{
	[EnumName("")]
	None = 0,

	[EnumName("H", "h")]
	HammerOn = 1,

	[EnumName("P", "p")]
	PullOff = 2,

	[EnumName("B", "b")]
	Bend = 4,

	[EnumName("R", "r")]
	Release = 8,

	[EnumName("S", "s")]
	Slide = 16,

	[EnumName("T", "t")]
	Tap = 32,

	[EnumName("X", "x")]
	DeadNote = 64,

	[EnumName("V", "v")]
	Vibrato = 128,

	[EnumName("L", "l")]
	Legato = 256,

	[EnumName("M", "m")]
	Mute = 512,
}

public readonly record struct TabNote
{
	public static readonly TabNote Empty = default;

	private readonly int value;

	public int? Value => value == 0 ? null : value - 1;

	public TabNoteModifier Modifier { get; }

	[MemberNotNullWhen(false, nameof(Value))]
	public bool IsEmpty => value == 0 && Modifier == TabNoteModifier.None;

	public TabNote(int? value, TabNoteModifier modifier = TabNoteModifier.None)
	{
		if (value < 0)
			throw new ArgumentOutOfRangeException(nameof(value), "Note kann nicht kleiner als Null sein");

		this.value = value.GetValueOrDefault(-1) + 1;
		this.Modifier = modifier;
	}

	public TabNote TriggerModifier(TabNoteModifier modifier)
		=> new(Value, Modifier ^ modifier);

	public override string ToString()
		=> IsEmpty ? "-"
		: Value is null ? string.Join(string.Empty, Modifier.GetFlagsDisplayName())
		: Modifier == TabNoteModifier.None ? Value.Value.ToString()
		: $"{string.Join(string.Empty, Modifier.GetFlagsDisplayName())}{Value.Value}";
}