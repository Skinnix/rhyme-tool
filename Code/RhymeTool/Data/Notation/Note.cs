using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Skinnix.RhymeTool;

namespace Skinnix.RhymeTool.Data.Notation;

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

public readonly record struct Note(NoteType Type, AccidentalType Accidental)
{
	public static readonly Note C = new(NoteType.C, AccidentalType.None);
	public static readonly Note CSharp = new(NoteType.C, AccidentalType.Sharp);
	public static readonly Note DFlat = new(NoteType.D, AccidentalType.Flat);
	public static readonly Note D = new(NoteType.D, AccidentalType.None);
	public static readonly Note DSharp = new(NoteType.D, AccidentalType.Sharp);
	public static readonly Note EFlat = new(NoteType.E, AccidentalType.Flat);
	public static readonly Note E = new(NoteType.E, AccidentalType.None);
	public static readonly Note F = new(NoteType.F, AccidentalType.None);
	public static readonly Note FSharp = new(NoteType.F, AccidentalType.Sharp);
	public static readonly Note GFlat = new(NoteType.G, AccidentalType.Flat);
	public static readonly Note G = new(NoteType.G, AccidentalType.None);
	public static readonly Note GSharp = new(NoteType.G, AccidentalType.Sharp);
	public static readonly Note AFlat = new(NoteType.A, AccidentalType.Flat);
	public static readonly Note A = new(NoteType.A, AccidentalType.None);
	public static readonly Note ASharp = new(NoteType.A, AccidentalType.Sharp);
	public static readonly Note BFlat = new(NoteType.B, AccidentalType.Flat);
	public static readonly Note B = new(NoteType.B, AccidentalType.None);

    public override string ToString()
        => Accidental == AccidentalType.None
        ? Type.GetDisplayName()
        : $"{Type.GetDisplayName()}{Accidental.GetDisplayName()}";

    public string ToString(ISheetFormatter? formatter)
        => formatter?.Format(this) ?? ToString();

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