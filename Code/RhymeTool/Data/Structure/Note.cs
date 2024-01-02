using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Structure;

public enum NoteType : byte
{
	[EnumName("C")]
	C,

	[EnumName("D")]
	D,

	[EnumName("E")]
	E,

	[EnumName("F")]
	F,

	[EnumName("G")]
	G,

	[EnumName("A")]
	A,

	[EnumName("B", "H")]
	B,
}

public enum AccidentalType : byte
{
	[EnumName("")]
	None,

	[EnumName("♯", "#")]
	Sharp,

	[EnumName("♭", "b")]
	Flat,
}

public record struct Note(NoteType Type, AccidentalType Accidental)
{
	public override string ToString()
		=> Accidental == AccidentalType.None
		? Type.GetDisplayName()
		: $"{Type.GetDisplayName()}{Accidental.GetDisplayName()}";

	public static int TryRead(ReadOnlySpan<char> s, out Note note)
	{
		note = default;
		if (s.IsEmpty)
			return -1;

		//Notenname
		var typeLength = EnumExtensions.TryRead(s, out NoteType type);
		if (typeLength == -1)
			return -1;
		var offset = typeLength;

		//Vorzeichen
		var accidentalLength = EnumExtensions.TryRead<AccidentalType>(s[offset..], out var accidental);
		if (accidentalLength != -1)
			offset += accidentalLength;

		note = new Note(type, accidental);
		return offset;
	}
}
