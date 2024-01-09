using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Skinnix.RhymeTool;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetFormatter
{
    string Format(Note note);
    string FormatBass(Note value);
    string Format(AccidentalType accidental);

    string Format(Chord chord);
    string Format(ChordQuality quality);
    string Format(ChordDegree chordDegree);
    string Format(ChordDegreeModifier modifier);

    string Format(ChordAlteration alteration);
    string Format(ChordAlterationType type);

    int SpaceBefore(SheetLine line, SheetDisplayLineBuilder lineBuilder, SheetDisplayLineElement element);
}

public enum GermanNoteMode
{
    AlwaysB,
    German,
    AlwaysH,
    Descriptive,
    ExplicitB,
    ExplicitH,
}

public record DefaultSheetFormatter : ISheetFormatter
{
    public static readonly DefaultSheetFormatter Instance = new();

    public static string SharpModifier { get; } = "♯";
    public static string FlatModifier { get; } = "♭";
    public static string NaturalModifier { get; } = "♮";

    public string DefaultAccidentalModifier { get; init; } = string.Empty;
    public string SharpAccidentalModifier { get; init; } = "♯";
    public string FlatAccidentalModifier { get; init; } = "♭";
    public string ExplicitNaturalModifier { get; init; } = "♮";

    public string MajorQuality { get; init; } = string.Empty;
    public string MinorQuality { get; init; } = "m";
    public string DiminishedQuality { get; init; } = "0";
    public string AugmentedQuality { get; init; } = "+";

    public string DefaultAlteration { get; init; } = string.Empty;
    public string AdditionAlteration { get; init; } = "add";
    public string SuspensionAlteration { get; init; } = "sus";

    public string DefaultDegreeModifier { get; init; } = string.Empty;
    public string SharpDegreeModifier { get; init; } = "♯";
    public string FlatDegreeModifier { get; init; } = "♭";
    public string MajorDegreeModifier { get; init; } = "maj";
    public string MajorSeventhDegreeModifier { get; init; } = "Δ";

    public GermanNoteMode GermanMode { get; init; } = GermanNoteMode.AlwaysB;

    public int SpaceBetweenChordsOnCompositeLine { get; init; } = 1;
    public int SpaceBetweenChordsOnChordLine { get; init; } = 3;

    public SheetTransformation? Transformation { get; init; }

    public string Format(AccidentalType accidental) => Format(accidental, true);
    private string Format(AccidentalType accidental, bool transform) => accidental switch
    {
        AccidentalType.None => string.Empty,
        AccidentalType.Sharp => SharpAccidentalModifier,
        AccidentalType.Flat => FlatAccidentalModifier,
        _ => throw new NotImplementedException("unknown accidental type"),
    };

    public string Format(ChordQuality quality) => Format(quality, true);
    private string Format(ChordQuality quality, bool transform) => quality switch
    {
        ChordQuality.Major => MajorQuality,
        ChordQuality.Minor => MinorQuality,
        ChordQuality.Diminished => DiminishedQuality,
        ChordQuality.Augmented => AugmentedQuality,
        _ => throw new NotImplementedException("unknown chord quality"),
    };

    public string Format(ChordAlterationType type) => Format(type, true);
    private string Format(ChordAlterationType type, bool transform) => type switch
    {
        ChordAlterationType.Default => DefaultAlteration,
        ChordAlterationType.Addition => AdditionAlteration,
        ChordAlterationType.Suspension => SuspensionAlteration,
        _ => throw new NotImplementedException("unknown chord alteration type"),
    };

    public string Format(ChordDegreeModifier modifier) => Format(modifier, true);
    private string Format(ChordDegreeModifier modifier, bool transform) => modifier switch
    {
        ChordDegreeModifier.None => string.Empty,
        ChordDegreeModifier.Sharp => SharpDegreeModifier,
        ChordDegreeModifier.Flat => FlatDegreeModifier,
        ChordDegreeModifier.Major => MajorDegreeModifier,
        _ => throw new NotImplementedException("unknown chord degree modifier"),
    };

    public string Format(Chord chord) => Format(chord, true);
    private string Format(Chord chord, bool transform)
    {
        if (transform && Transformation != null)
            chord = Transformation.TransformChord(chord);

        var sb = new StringBuilder();
        sb.Append(Format(chord.Root, false));
        sb.Append(Format(chord.Quality, false));

        sb.Append(string.Join('/', chord.Alterations.Select(a => Format(a, false))));

        if (chord.Bass is not null)
            sb.Append('/').Append(FormatBass(chord.Bass.Value, false));

        return sb.ToString();
    }

    public string Format(ChordAlteration alteration) => Format(alteration, true);
    private string Format(ChordAlteration alteration, bool transform)
        => Format(alteration.Type) + alteration.Degree;

    public string Format(ChordDegree chordDegree) => Format(chordDegree, true);
    private string Format(ChordDegree chordDegree, bool transform)
    {
        if (chordDegree is (7, ChordDegreeModifier.Major) && MajorSeventhDegreeModifier != null)
            return MajorSeventhDegreeModifier;

        if (chordDegree.Modifier == ChordDegreeModifier.None)
            return chordDegree.Value.ToString();

        return Format(chordDegree.Modifier) + chordDegree.Value;
    }

    public string FormatBass(Note value) => FormatBass(value, true);
    private string FormatBass(Note value, bool transform) => Format(value, transform);
    public string Format(Note note) => Format(note, true);
    private string Format(Note note, bool transform)
    {
        if (transform && Transformation != null)
            note = Transformation.TransformNote(note);

        //Germanize
        if (note.Type == NoteType.B)
        {
            switch (GermanMode)
            {
                case GermanNoteMode.AlwaysH:
                    return "H" + Format(note.Accidental);
                case GermanNoteMode.German:
                    if (note.Accidental == AccidentalType.Flat)
                        return "B";
                    else
                        return "H" + Format(note.Accidental);
                case GermanNoteMode.Descriptive:
                    if (note.Accidental == AccidentalType.Flat)
                        return "B" + Format(note.Accidental);
                    else
                        return "H" + Format(note.Accidental);
                case GermanNoteMode.ExplicitB:
                    return note.Accidental switch
                    {
                        AccidentalType.None => "B" + ExplicitNaturalModifier,
                        AccidentalType.Sharp => "B" + SharpAccidentalModifier,
                        AccidentalType.Flat => "B" + FlatAccidentalModifier,
                        _ => throw new NotImplementedException("unknown accidental type"),
                    };
                case GermanNoteMode.ExplicitH:
                    return note.Accidental switch
                    {
                        AccidentalType.None => "H" + ExplicitNaturalModifier,
                        AccidentalType.Sharp => "H" + SharpAccidentalModifier,
                        AccidentalType.Flat => "H" + FlatAccidentalModifier,
                        _ => throw new NotImplementedException("unknown accidental type"),
                    };

            }
        }

        return note.Type.GetDisplayName() + Format(note.Accidental);
    }

    public int SpaceBefore(SheetLine line, SheetDisplayLineBuilder lineBuilder, SheetDisplayLineElement element)
    {
        if (lineBuilder.CurrentLength > 0 && element is SheetDisplayLineChord)
        {
            if (line is SheetCompositeLine)
                return SpaceBetweenChordsOnCompositeLine;
            else
                return 1;
        }

        return 0;
    }
}

public static class SheetFormatterExceptions
{
    public static string ToString(this AccidentalType accidental, ISheetFormatter? formatter)
        => formatter?.Format(accidental) ?? accidental.GetDisplayName();

    public static string ToString(this ChordQuality quality, ISheetFormatter? formatter)
        => formatter?.Format(quality) ?? quality.GetDisplayName();

    public static string ToString(this ChordAlterationType type, ISheetFormatter? formatter)
        => formatter?.Format(type) ?? type.GetDisplayName();

    public static string ToString(this ChordDegreeModifier modifier, ISheetFormatter? formatter)
        => formatter?.Format(modifier) ?? modifier.GetDisplayName();
}