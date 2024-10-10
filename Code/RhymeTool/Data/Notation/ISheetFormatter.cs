using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct NoteFormat(string Type, string? Accidental = null)
{
	public override string ToString() => Type.ToString() + Accidental?.ToString();
}

public readonly record struct AlterationFormat(string Type, string Degree, string Modifier, bool ModifierAfter)
{
	public override string ToString() => Type + (ModifierAfter ? Degree + Modifier : Modifier + Degree);
}

public interface ISheetFormatter
{
	string ToString(Note note);

	NoteFormat Format(Note note);

	string ToString(Chord chord);
	string ToString(ChordQuality quality);

	string ToString(ChordAlteration alteration, int index);
	string ToString(ChordAlterationType type);

	AlterationFormat Format(ChordAlteration alteration, int index);

	string ToString(Fingering fingering);
}

public interface ISheetBuilderFormatter : ISheetFormatter
{
	IEnumerable<int> GetLineIndentations();

	int SpaceBefore(SheetLine line, SheetDisplayLineBuilderBase lineBuilder, SheetDisplayLineElement element);
	bool ShowLine(SheetLine line, SheetDisplayLineBuilderBase lineBuilder);
	void AfterPopulateLine(SheetLine line, SheetDisplayLineBuilderBase lineBuilder, IEnumerable<SheetDisplayLineBuilderBase> allLines);
}

public interface ISheetEditorFormatter : ISheetBuilderFormatter
{
	int TryReadChord(ReadOnlySpan<char> s, out Chord? chord);
	int TryReadFingering(ReadOnlySpan<char> s, out Fingering? fingering, int minLength = 1);
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

public record DefaultSheetFormatter : ISheetEditorFormatter
{
	public static readonly DefaultSheetFormatter Instance = new();

	public static string TextSharpModifier { get; } = "#";
	public static string TextFlatModifier { get; } = "b";
	public static string TextNaturalModifier { get; } = "♮";

	public static string SharpModifier { get; } = "♯";
	public static string FlatModifier { get; } = "♭";
	public static string NaturalModifier { get; } = "♮";

	public string DefaultAccidentalModifier { get; init; } = string.Empty;
	public string SharpAccidentalModifier { get; init; } = SharpModifier;
	public string FlatAccidentalModifier { get; init; } = FlatModifier;
	public string ExplicitNaturalAccidentalModifier { get; init; } = NaturalModifier;

	public string TextDefaultAccidentalModifier { get; init; } = string.Empty;
	public string TextSharpAccidentalModifier { get; init; } = TextSharpModifier;
	public string TextFlatAccidentalModifier { get; init; } = TextFlatModifier;
	public string TextExplicitNaturalAccidentalModifier { get; init; } = TextNaturalModifier;

	public string MajorQuality { get; init; } = string.Empty;
	public string MinorQuality { get; init; } = "m";
	public string DiminishedQuality { get; init; } = "0";
	public string AugmentedQuality { get; init; } = "+";

	public string DefaultAlteration { get; init; } = string.Empty;
	public string AdditionAlteration { get; init; } = "add";
	public string SuspensionAlteration { get; init; } = "sus";

	public string DefaultDegreeModifier { get; init; } = string.Empty;
	public string SharpDegreeModifier { get; init; } = SharpModifier;
	public string FlatDegreeModifier { get; init; } = FlatModifier;
	public string MajorDegreeModifier { get; init; } = "maj";
	public string MajorSeventhDegreeModifier { get; init; } = "Δ";

	public string TextDefaultDegreeModifier { get; init; } = string.Empty;
	public string TextSharpDegreeModifier { get; init; } = TextSharpModifier;
	public string TextFlatDegreeModifier { get; init; } = TextFlatModifier;
	public string TextMajorDegreeModifier { get; init; } = "maj";
	public string TextMajorSeventhDegreeModifier { get; init; } = "Δ";

	public GermanNoteMode GermanMode { get; init; } = GermanNoteMode.AlwaysB;

	public int SpaceBetweenChordsOnTextLine { get; init; } = 3;
	public int SpaceBetweenChordsOnChordLine { get; init; } = 1;
	public bool ExtendAttachmentLines { get; init; } = false;
	public bool ShowEmptyAttachmentLines { get; init; } = false;

	public List<int> LineIndentations { get; init; } = [0, 2];

	public SheetTransformation? Transformation { get; init; }

	public string ToString(ChordQuality quality) => ToString(quality, true);
	private string ToString(ChordQuality quality, bool transform) => quality switch
	{
		ChordQuality.Major => MajorQuality,
		ChordQuality.Minor => MinorQuality,
		ChordQuality.Diminished => DiminishedQuality,
		ChordQuality.Augmented => AugmentedQuality,
		_ => string.Empty
	};

	public string ToString(ChordAlterationType type) => ToString(type, true);
	private string ToString(ChordAlterationType type, bool transform) => type switch
	{
		ChordAlterationType.Default => DefaultAlteration,
		ChordAlterationType.Addition => AdditionAlteration,
		ChordAlterationType.Suspension => SuspensionAlteration,
		_ => string.Empty
	};

	public string ToString(Chord chord) => ToString(chord, true);
	private string ToString(Chord chord, bool transform)
	{
		if (transform && Transformation != null)
			chord = Transformation.TransformChord(chord);

		var sb = new StringBuilder();
		sb.Append(ToString(chord.Root, false));
		sb.Append(ToString(chord.Quality, false));

		sb.Append(string.Join('/', chord.Alterations.Select((a, i) => ToString(a, i, false))));

		if (chord.Bass is not null)
			sb.Append('/').Append(ToString(chord.Bass.Value, false));

		return sb.ToString();
	}

	public string ToString(ChordAlteration alteration, int index) => ToString(alteration, index, true);
	private string ToString(ChordAlteration alteration, int index, bool transform)
		=> ToString(alteration.Type) + FormatInAlteration(alteration.Degree, alteration, index, transform);
	private string FormatInAlteration(ChordDegree degree, ChordAlteration alteration, int alterationIndex, bool transform)
	{
		if (degree is (7, ChordDegreeModifier.Major) && MajorSeventhDegreeModifier != null)
			return MajorSeventhDegreeModifier;

		if (degree.Modifier == ChordDegreeModifier.None)
			return degree.Value.ToString();

		if (alterationIndex == 0)
			return degree.Value + ToString(degree.Modifier, false, transform);
		else
			return ToString(degree.Modifier, false, transform) + degree.Value;
	}

	private string ToString(ChordDegreeModifier modifier, bool inDocument, bool transform) => modifier switch
	{
		ChordDegreeModifier.None => inDocument ? DefaultDegreeModifier : TextDefaultDegreeModifier,
		ChordDegreeModifier.Sharp => inDocument ? SharpDegreeModifier : TextSharpDegreeModifier,
		ChordDegreeModifier.Flat => inDocument ? FlatDegreeModifier : TextFlatDegreeModifier,
		ChordDegreeModifier.Major => inDocument ? MajorDegreeModifier : TextMajorDegreeModifier,
		_ => string.Empty
	};

	public AlterationFormat Format(ChordAlteration alteration, int index)
		=> FormatAlteration(alteration, index, true);
	private AlterationFormat FormatAlteration(ChordAlteration alteration, int index, bool transform)
	{
		//Sonderfall: maj7
		if (alteration.Type == ChordAlterationType.Default && alteration.Degree is (7, ChordDegreeModifier.Major))
		{
			if (MajorSeventhDegreeModifier != null)
				return new(MajorSeventhDegreeModifier, string.Empty, string.Empty, false);
			else
				return new(ToString(alteration.Type, transform), alteration.Degree.Value.ToString(), ToString(alteration.Degree.Modifier, true, transform), false);
		}

		var type = ToString(alteration.Type, transform);
		return new(ToString(alteration.Type, transform), alteration.Degree.Value.ToString(), ToString(alteration.Degree.Modifier, true, transform), index == 0);
	}

	private string ToString(AccidentalType accidental, bool inDocument, bool transform) => accidental switch
	{
		AccidentalType.None => inDocument ? DefaultAccidentalModifier : TextDefaultAccidentalModifier,
		AccidentalType.Sharp => inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier,
		AccidentalType.Flat => inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier,
		_ => string.Empty
	};

	public string ToString(Note note) => ToString(note, true);
	private string ToString(Note note, bool transform) => Format(note, false, transform).ToString();
	public NoteFormat Format(Note note) => Format(note, true);
	private NoteFormat Format(Note note, bool transform) => Format(note, true, transform);

	private NoteFormat Format(Note note, bool inDocument, bool transform)
	{
		if (transform && Transformation != null)
			note = Transformation.TransformNote(note);

		//Germanize
		if (note.Type == NoteType.B)
			switch (GermanMode)
			{
				case GermanNoteMode.AlwaysH:
					return new("H", ToString(note.Accidental, inDocument, transform));
				case GermanNoteMode.German:
					if (note.Accidental == AccidentalType.Flat)
						return new("B");
					else
						return new("H", ToString(note.Accidental, inDocument, transform));
				case GermanNoteMode.Descriptive:
					if (note.Accidental == AccidentalType.Flat)
						return new("B", ToString(note.Accidental, inDocument, transform));
					else
						return new("H", ToString(note.Accidental, inDocument, transform));
				case GermanNoteMode.ExplicitB:
					return note.Accidental switch
					{
						AccidentalType.None => new("B", inDocument ? ExplicitNaturalAccidentalModifier : TextExplicitNaturalAccidentalModifier),
						AccidentalType.Sharp => new("B", inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier),
						AccidentalType.Flat => new("B", inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier),
						_ => throw new NotImplementedException("unknown accidental type"),
					};
				case GermanNoteMode.ExplicitH:
					return note.Accidental switch
					{
						AccidentalType.None => new("H", inDocument ? ExplicitNaturalAccidentalModifier : TextExplicitNaturalAccidentalModifier),
						AccidentalType.Sharp => new("H", inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier),
						AccidentalType.Flat => new("H", inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier),
						_ => throw new NotImplementedException("unknown accidental type"),
					};

			}

		return new(note.Type.GetDisplayName(), ToString(note.Accidental, inDocument, transform));
	}

	public string ToString(Fingering fingering) => string.Join("", fingering.Positions.Select(p => p.ToString()));

	public IEnumerable<int> GetLineIndentations() => LineIndentations;

	public int SpaceBefore(SheetLine line, SheetDisplayLineBuilderBase lineBuilder, SheetDisplayLineElement element)
	{
		if (lineBuilder.CurrentLength > 0 && element is SheetDisplayLineChord)
			if (lineBuilder.LineType == typeof(SheetDisplayTextLine))
				return SpaceBetweenChordsOnTextLine;
			else if (lineBuilder.LineType == typeof(SheetDisplayChordLine))
				return SpaceBetweenChordsOnChordLine;
			else
				return 1;

		return 0;
	}

	public void AfterPopulateLine(SheetLine line, SheetDisplayLineBuilderBase lineBuilder, IEnumerable<SheetDisplayLineBuilderBase> allLines)
	{
		if (ExtendAttachmentLines && lineBuilder is SheetDisplayChordLine.Builder chordBuilder)
		{
			//Verlängere die Zeile auf die länge der längsten Zeile - 1
			var length = allLines.Max(l => l.CurrentLength) - 1;
			chordBuilder.ExtendLength(length, 0, this);
		}
	}

	public bool ShowLine(SheetLine line, SheetDisplayLineBuilderBase lineBuilder)
	{
		if (!ShowEmptyAttachmentLines && lineBuilder.CurrentNonSpaceLength == 0 && lineBuilder is SheetDisplayChordLine.Builder)
			return false;

		return true;
	}

	public int TryReadChord(ReadOnlySpan<char> s, out Chord? chord) => TryReadChord(s, out chord, true);
	public int TryReadChord(ReadOnlySpan<char> s, out Chord? chord, bool transform)
	{
		var length = Chord.TryRead(s, out chord);
		if (length <= 0 || chord is null)
		{
			chord = null;
			return 0;
		}

		if (chord.Root.Type == NoteType.B)
			switch (GermanMode)
			{
				case GermanNoteMode.German:
					if (chord.Root.Accidental == AccidentalType.None && s.StartsWith("B", StringComparison.OrdinalIgnoreCase))
						chord = chord with
						{
							Root = new(NoteType.B, AccidentalType.Flat)
						};
					break;
			}

		if (chord.Bass?.Type == NoteType.B)
			switch (GermanMode)
			{
				case GermanNoteMode.German:
					if (chord.Root.Accidental == AccidentalType.None && s.StartsWith("B", StringComparison.OrdinalIgnoreCase))
						chord = chord with
						{
							Bass = new(NoteType.B, AccidentalType.Flat)
						};
					break;
			}

		if (transform && Transformation is not null)
			chord = Transformation.UntransformChord(chord);

		return length;
	}

	public int TryReadFingering(ReadOnlySpan<char> s, out Fingering? fingering, int minLength = 2)
		=> Fingering.TryRead(s, out fingering, minLength);
}

public static class SheetFormatterExtensions
{
	public static string ToString(this ChordQuality quality, ISheetFormatter? formatter)
		=> formatter?.ToString(quality) ?? quality.GetDisplayName();

	public static string ToString(this ChordAlterationType type, ISheetFormatter? formatter)
		=> formatter?.ToString(type) ?? type.GetDisplayName();
}
