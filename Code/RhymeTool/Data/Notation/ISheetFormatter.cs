using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public record struct NoteFormat(string Type, string? Accidental = null)
{
	public override string ToString() => Type.ToString() + Accidental?.ToString();
}

public interface ISheetFormatter
{
	string Format(Note note);
	string FormatBass(Note value);
	string Format(AccidentalType accidental);

	NoteFormat FormatNote(Note note);
	NoteFormat FormatBassNote(Note note);

	string Format(Chord chord);
	string Format(ChordQuality quality);
	string Format(ChordDegree chordDegree);
	string Format(ChordDegreeModifier modifier);

	string Format(ChordAlteration alteration);
	string Format(ChordAlterationType type);

	string Format(Fingering fingering);
}

public interface ISheetBuilderFormatter : ISheetFormatter
{
	IEnumerable<int> GetLineIndentations();

	int SpaceBefore(SheetLine line, SheetDisplayLineBuilder lineBuilder, SheetDisplayLineElement element);
	bool ShowLine(SheetLine line, SheetDisplayLineBuilder lineBuilder);
	void AfterPopulateLine(SheetLine line, SheetDisplayLineBuilder lineBuilder, IEnumerable<SheetDisplayLineBuilder> allLines);
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

	public static string SharpModifier { get; } = "♯";
	public static string FlatModifier { get; } = "♭";
	public static string NaturalModifier { get; } = "♮";

	public string DefaultAccidentalModifier { get; init; } = string.Empty;
	public string SharpAccidentalModifier { get; init; } = SharpModifier;
	public string FlatAccidentalModifier { get; init; } = FlatModifier;
	public string ExplicitNaturalAccidentalModifier { get; init; } = NaturalModifier;

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

	public GermanNoteMode GermanMode { get; init; } = GermanNoteMode.AlwaysB;

	public int SpaceBetweenChordsOnTextLine { get; init; } = 3;
	public int SpaceBetweenChordsOnChordLine { get; init; } = 1;
	public bool ExtendAttachmentLines { get; init; } = false;
	public bool ShowEmptyAttachmentLines { get; init; } = false;

	public List<int> LineIndentations { get; init; } = [0, 2];

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
	public NoteFormat FormatBassNote(Note note) => FormatBassNote(note, true);
	private NoteFormat FormatBassNote(Note note, bool transform) => FormatNote(note, transform);

	public string Format(Note note) => Format(note, true);
	private string Format(Note note, bool transform) => FormatNote(note, transform).ToString();
	public NoteFormat FormatNote(Note note) => FormatNote(note, true);
	private NoteFormat FormatNote(Note note, bool transform)
	{
		if (transform && Transformation != null)
			note = Transformation.TransformNote(note);

		//Germanize
		if (note.Type == NoteType.B)
			switch (GermanMode)
			{
				case GermanNoteMode.AlwaysH:
					return new("H", Format(note.Accidental));
				case GermanNoteMode.German:
					if (note.Accidental == AccidentalType.Flat)
						return new("B");
					else
						return new("H", Format(note.Accidental));
				case GermanNoteMode.Descriptive:
					if (note.Accidental == AccidentalType.Flat)
						return new("B", Format(note.Accidental));
					else
						return new("H", Format(note.Accidental));
				case GermanNoteMode.ExplicitB:
					return note.Accidental switch
					{
						AccidentalType.None => new("B", ExplicitNaturalAccidentalModifier),
						AccidentalType.Sharp => new("B", SharpAccidentalModifier),
						AccidentalType.Flat => new("B", FlatAccidentalModifier),
						_ => throw new NotImplementedException("unknown accidental type"),
					};
				case GermanNoteMode.ExplicitH:
					return note.Accidental switch
					{
						AccidentalType.None => new("H", ExplicitNaturalAccidentalModifier),
						AccidentalType.Sharp => new("H", SharpAccidentalModifier),
						AccidentalType.Flat => new("H", FlatAccidentalModifier),
						_ => throw new NotImplementedException("unknown accidental type"),
					};

			}

		return new(note.Type.GetDisplayName(), Format(note.Accidental));
	}

	public string Format(Fingering fingering) => string.Join("", fingering.Positions.Select(p => p.ToString()));

	public IEnumerable<int> GetLineIndentations() => LineIndentations;

	public int SpaceBefore(SheetLine line, SheetDisplayLineBuilder lineBuilder, SheetDisplayLineElement element)
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

	public void AfterPopulateLine(SheetLine line, SheetDisplayLineBuilder lineBuilder, IEnumerable<SheetDisplayLineBuilder> allLines)
	{
		if (ExtendAttachmentLines && lineBuilder is SheetDisplayChordLine.Builder)
		{
			//Verlängere die Zeile auf die länge der längsten Zeile - 1
			var length = allLines.Max(l => l.CurrentLength) - 1;
			lineBuilder.ExtendLength(length, 0, this);
		}
	}

	public bool ShowLine(SheetLine line, SheetDisplayLineBuilder lineBuilder)
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