using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

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
	public const char EMPTY_CHAR = '-';

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

	public static int TryRead(ReadOnlySpan<char> s, out TabNote note, int maxNumberLength = 2, ISheetEditorFormatter? formatter = null)
	{
		if (formatter is not null)
			return formatter.TryReadTabNote(s, out note, maxNumberLength);

		note = Empty;
		if (s.Length == 0)
			return -1;

		if (s[0] == EMPTY_CHAR)
			return 1;

		var modifierLength = EnumNameAttribute.TryRead<TabNoteModifier>(s, out var modifier);
		if (modifierLength > 0)
			s = s[modifierLength..];

		//Versuche Zahl zu lesen
		for (var i = Math.Min(maxNumberLength, s.Length); i > 0; i--)
		{
			if (byte.TryParse(s[0..i], out var noteValue))
			{
				note = new TabNote(noteValue, modifier);
				return modifierLength <= 0 ? i : modifierLength + i;
			}
		}

		//Keine Zahl gelesen
		note = new TabNote(null, modifier);
		return modifierLength;
	}

	public TabNote TriggerModifier(TabNoteModifier modifier)
		=> new(Value, Modifier ^ modifier);

	public override string ToString()
		=> IsEmpty ? EMPTY_CHAR.ToString()
		: Value is null ? string.Join(string.Empty, Modifier.GetFlagsDisplayName())
		: Modifier == TabNoteModifier.None ? Value.Value.ToString()
		: $"{string.Join(string.Empty, Modifier.GetFlagsDisplayName())}{Value.Value}";

	public string ToString(ISheetFormatter? formatter, TabColumnWidth width)
		=> formatter?.ToString(this, width)
		?? ToString();

	public TabNoteFormat Format(TabColumnWidth width, ISheetFormatter? formatter = null)
		=> (formatter ?? DefaultSheetFormatter.Instance).Format(this, width);

	public readonly record struct SimpleTabNoteFormat(string Text, TabColumnWidth Width,
		string? Prefix = null, string? Suffix = null)
	{
		public int TotalTextLength => Text.Length + (Prefix?.Length ?? 0) + (Suffix?.Length ?? 0);

		public override string ToString() => Text;
	}

	public readonly record struct TabNoteFormat(TabNote Note,
		string Text, TabColumnWidth Width, string? Prefix = null, string? Suffix = null)
	{
		public int TotalTextLength => Text.Length + (Prefix?.Length ?? 0) + (Suffix?.Length ?? 0);

		public override string ToString() => Text;

		public static implicit operator SimpleTabNoteFormat(TabNoteFormat format)
			=> new(format.Text, format.Width, format.Prefix, format.Suffix);
	}

	public readonly record struct TabNoteTuningFormat(Note Note,
		string Text, TabColumnWidth Width, string? Prefix = null, string? Suffix = null)
	{
		public int TotalTextLength => Text.Length + (Prefix?.Length ?? 0) + (Suffix?.Length ?? 0);

		public override string ToString() => Text;

		public static implicit operator SimpleTabNoteFormat(TabNoteTuningFormat format)
			=> new(format.Text, format.Width, format.Prefix, format.Suffix);
	}
}
