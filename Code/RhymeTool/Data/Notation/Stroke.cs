namespace Skinnix.RhymeTool.Data.Notation;

public enum StrokeType
{
	[EnumName(" ", "·")]
	None,

	[EnumName("v", "↓", "d")]
	Down,

	[EnumName("^", "↑", "u")]
	Up,

	[EnumName(",", "⇣")]
	LightDown,

	[EnumName("'", ";", "⇡")]
	LightUp,

	[EnumName("m", "_", "⤈")]
	MuteDown,

	[EnumName("M", "°", "⤉")]
	MuteUp,

	[EnumName("-", "—")]
	Hold,

	[EnumName(".", "/")]
	Rest,

	[EnumName("x", "×")]
	DeadNote,
}

public readonly record struct Stroke(StrokeType Type)
{
	public override string ToString() => ToString(null);

	public string ToString(ISheetFormatter? formatter)
		=> formatter?.ToString(this)
		?? Type.GetDisplayName();
}
