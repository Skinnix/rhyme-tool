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
	public StrokeFormat Format(ISheetFormatter? formatter)
		=> (formatter ?? DefaultSheetFormatter.Instance).Format(this);

	public override string ToString() => ToString(null);

	public string ToString(ISheetFormatter? formatter)
		=> (formatter ?? DefaultSheetFormatter.Instance).ToString(this);

	public readonly record struct StrokeFormat(Stroke Stroke,
		string Type, int? Length = null, NoteLength? NoteLength = default)
	{
		public override string ToString() => Type;
	}
}
