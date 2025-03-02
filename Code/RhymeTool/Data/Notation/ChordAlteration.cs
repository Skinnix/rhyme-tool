﻿using System.ComponentModel.DataAnnotations;
using Skinnix.RhymeTool;

namespace Skinnix.RhymeTool.Data.Notation;

public enum ChordDegreeModifier
{
    [EnumName("")]
    None,

    [EnumName("♯", "#")]
    Sharp,

    [EnumName("♭", "b")]
    Flat,

    [EnumName("maj", "M")]
    Major,
}

public readonly record struct ChordDegree(byte Value, ChordDegreeModifier Modifier = ChordDegreeModifier.None)
{
	public static char MAJOR_SEVENTH_SUFFIX = 'Δ';

    public override string ToString()
        => Modifier == ChordDegreeModifier.None
        ? Value.ToString()
        : $"{Modifier.GetDisplayName()}{Value}";

    public static int TryRead(ReadOnlySpan<char> s, out ChordDegree degree)
    {
        degree = default;
        if (s.IsEmpty) return -1;

        //Lese Modifier
        var modifierLength = EnumExtensions.TryRead(s, out ChordDegreeModifier modifier);
		int offset;
		if (modifierLength != -1)
		{
			offset = modifierLength;
		}
		else if (s.Length > 0 && s[0] == MAJOR_SEVENTH_SUFFIX)
		{
			//Damit ist der Degree ein Major Seventh
			degree = new(7, ChordDegreeModifier.Major);
			return 1;
		}
		else
		{
			offset = 0;
		}

        //Lese Stufe
        if (s.Length <= offset) return -1;
        if (s.Length >= offset + 2 && byte.TryParse(s[offset..(offset + 2)], System.Globalization.NumberStyles.None, provider: null, out var value))
        {
            offset += 2;
        }
        else if (byte.TryParse(s[offset..(offset + 1)], System.Globalization.NumberStyles.None, provider: null, out value))
        {
            offset += 1;
        }
        else
        {
            //Keine Stufe angegeben
            return -1;
        }

        //Lese ggf. Modifier am Ende, falls er nicht am Anfang stand
        if (modifierLength == -1)
        {
            modifierLength = EnumExtensions.TryRead(s[offset..], out modifier);
            if (modifierLength != -1)
                offset += modifierLength;
        }

        //Erfolgreich gelesen
        degree = new(value, modifier);
        return offset;
    }
}

public enum ChordAlterationType
{
    [EnumName("")]
    Default,

    [EnumName("add")]
    Addition,

    [EnumName("sus")]
    Suspension,
}

public record struct ChordAlteration(ChordAlterationType Type, ChordDegree Degree)
{
	public AlterationFormat Format(int index, ISheetFormatter? formatter = null)
		=> (formatter ?? DefaultSheetFormatter.Instance).Format(this, index);

	public override string ToString()
		=> ToString(0, null);

    public string ToString(int index = 0, ISheetFormatter? formatter = null)
		=> (formatter ?? DefaultSheetFormatter.Instance).ToString(this, index);

    public static int TryRead(ReadOnlySpan<char> s, out ChordAlteration alteration)
    {
        alteration = default;
        if (s.IsEmpty)
            return -1;

        //Lese Alterationstyp
        var typeLength = EnumExtensions.TryRead(s, out ChordAlterationType type);
        var offset = typeLength == -1 ? 0 : typeLength;

        //Lese Stufe
        var degreeLength = ChordDegree.TryRead(s[offset..], out var degree);
        if (degreeLength == -1) return -1;
        offset += degreeLength;

        alteration = new ChordAlteration(type, degree);
        return offset;
    }

	public readonly record struct AlterationFormat(ChordAlteration Alteration,
		string Type, string Degree, string Modifier, bool ModifierAfter)
	{
		public override string ToString() => Type + (ModifierAfter ? Degree + Modifier : Modifier + Degree);
	}
}