using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct NoteFormat(string Type, string? Accidental = null, AccidentalType AccidentalType = AccidentalType.None)
{
	public override string ToString() => Type.ToString() + Accidental?.ToString();
}

public readonly record struct AlterationFormat(string Type, string Degree, string Modifier, bool ModifierAfter)
{
	public override string ToString() => Type + (ModifierAfter ? Degree + Modifier : Modifier + Degree);
}

public readonly record struct RhythmPatternFormat(string LeftDelimiter, string RightDelimiter, StrokeFormat[] Strokes)
{
	public override string ToString() => LeftDelimiter + string.Join(null, Strokes) + RightDelimiter;
}

public readonly record struct StrokeFormat(string Stroke, StrokeType Type, int? Length = null, NoteLength? NoteLength = default)
{
	public override string ToString() => Stroke;
}

public readonly record struct TabNoteFormat(string Text, int Width, string? Prefix = null, string? Suffix = null)
{
	public int TotalTextLength => Text.Length + (Prefix?.Length ?? 0) + (Suffix?.Length ?? 0);

	public override string ToString() => Text;
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

	string ToString(NoteLength noteLength);

	string ToString(Stroke stroke);
	string ToString(RhythmPattern pattern);
	RhythmPatternFormat Format(RhythmPattern pattern);

	string ToString(TabNote note, int width);
	TabNoteFormat Format(TabNote note, int width);

	string ToString(Note tuning, int width);
	TabNoteFormat Format(Note tuning, int width);
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
	bool KeepTabLineSelection { get; }

	SheetTransformation? Transformation { get; }

	int TryReadNote(ReadOnlySpan<char> s, out Note note);
	int TryReadAccidental(ReadOnlySpan<char> s, out AccidentalType accidental);
	int TryReadChord(ReadOnlySpan<char> s, out Chord? chord);
	int TryReadFingering(ReadOnlySpan<char> s, out Fingering? fingering, int minLength = 1);
	int TryReadRhythm(ReadOnlySpan<char> s, out RhythmPattern? rhythm);
	int TryReadTabNoteModifier(ReadOnlySpan<char> s, out TabNoteModifier modifier);
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

	public string RhythmPatternLeftDelimiter { get; init; } = "|";
	public string RhythmPatternRightDelimiter { get; init; } = "|";

	public string TextRhythmPatternLeftDelimiter { get; init; } = "|";
	public string TextRhythmPatternRightDelimiter { get; init; } = "|";

	public IReadOnlyList<string> StrokeTypes { get; init; } = new List<string>(Enum.GetNames<StrokeType>())
	{
		[(int)StrokeType.None]      = " ", // "·",
		[(int)StrokeType.Down]      = "↓",
		[(int)StrokeType.Up]        = "↑",
		[(int)StrokeType.LightDown] = "⇣",
		[(int)StrokeType.LightUp]   = "⇡",
		[(int)StrokeType.MuteDown]  = "⤈",
		[(int)StrokeType.MuteUp]    = "⤉",
		[(int)StrokeType.Hold]      = "—",
		[(int)StrokeType.Rest]      = "/",
		[(int)StrokeType.DeadNote]  = "×",
	};

	public IReadOnlyList<string> TextStrokeTypes { get; init; } = new List<string>(Enum.GetNames<StrokeType>())
	{
		[(int)StrokeType.None]      = " ",
		[(int)StrokeType.Down]      = "v",
		[(int)StrokeType.Up]        = "^",
		[(int)StrokeType.LightDown] = ",",
		[(int)StrokeType.LightUp]   = "'",
		[(int)StrokeType.MuteDown]  = "m",
		[(int)StrokeType.MuteUp]    = "M",
		[(int)StrokeType.Hold]      = "-",
		[(int)StrokeType.Rest]      = ".",
		[(int)StrokeType.DeadNote]  = "x",
	};

	public string[] NoteLengths { get; init; } = [" ", "𝅝", "𝅗𝅥", "𝅘𝅥", "𝅘𝅥𝅮", "𝅘𝅥𝅯", "𝅘𝅥𝅰", "𝅘𝅥𝅱", "𝅘𝅥𝅲"];
	public string[] RestLengths { get; init; } = [" ", "𝄻", "𝄼", "𝄽", "𝄾", "𝄿", "𝅀", "𝅁", "𝅂"];
	public char NoteLengthDot { get; init; } = '·';

	public bool CondenseTabNotes { get; init; } = true;

	public GermanNoteMode GermanMode { get; init; } = GermanNoteMode.AlwaysB;

	public int SpaceBetweenChordsOnTextLine { get; init; } = 3;
	public int SpaceBetweenChordsOnChordLine { get; init; } = 1;
	public bool ExtendAttachmentLines { get; init; } = true;
	public bool ShowEmptyAttachmentLines { get; init; } = false;

	public bool KeepTabLineSelection { get; init; } = true;

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

		if (alterationIndex == 0 && alteration.Degree.Modifier is ChordDegreeModifier.Sharp or ChordDegreeModifier.Flat)
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
		var modifierAfter = index == 0
			&& alteration.Degree.Modifier is ChordDegreeModifier.Sharp or ChordDegreeModifier.Flat;
		return new(ToString(alteration.Type, transform), alteration.Degree.Value.ToString(), ToString(alteration.Degree.Modifier, true, transform), modifierAfter);
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
					return new("H", ToString(note.Accidental, inDocument, transform), note.Accidental);
				case GermanNoteMode.German:
					if (note.Accidental == AccidentalType.Flat)
						return new("B", null, AccidentalType.None);
					else
						return new("H", ToString(note.Accidental, inDocument, transform), note.Accidental);
				case GermanNoteMode.Descriptive:
					if (note.Accidental == AccidentalType.Flat)
						return new("B", ToString(note.Accidental, inDocument, transform), note.Accidental);
					else
						return new("H", ToString(note.Accidental, inDocument, transform), note.Accidental);
				case GermanNoteMode.ExplicitB:
					return note.Accidental switch
					{
						AccidentalType.None => new("B", inDocument ? ExplicitNaturalAccidentalModifier : TextExplicitNaturalAccidentalModifier, note.Accidental),
						AccidentalType.Sharp => new("B", inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier, note.Accidental),
						AccidentalType.Flat => new("B", inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier, note.Accidental),
						_ => throw new NotImplementedException("unknown accidental type"),
					};
				case GermanNoteMode.ExplicitH:
					return note.Accidental switch
					{
						AccidentalType.None => new("H", inDocument ? ExplicitNaturalAccidentalModifier : TextExplicitNaturalAccidentalModifier, note.Accidental),
						AccidentalType.Sharp => new("H", inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier, note.Accidental),
						AccidentalType.Flat => new("H", inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier, note.Accidental),
						_ => throw new NotImplementedException("unknown accidental type"),
					};

			}

		return new(note.Type.GetDisplayName(), ToString(note.Accidental, inDocument, transform), note.Accidental);
	}

	public string ToString(Fingering fingering)
		=> string.Join(null, fingering.Positions.Select(p => p.ToString()));

	public string ToString(NoteLength noteLength)
	{
		var strings = noteLength.IsRest ? RestLengths : NoteLengths;
		var s = noteLength.Values.Select(v => v >= 0 && (int)v <= (strings.Length - 1) ? strings[(int)v] : " ");
		return string.Join(null, s) + new string(NoteLengthDot, noteLength.Dots);
	}

	public string ToString(Stroke stroke)
		=> TextStrokeTypes[(int)stroke.Type];

	public string ToString(RhythmPattern pattern)
		=> TextRhythmPatternLeftDelimiter + string.Join(null, pattern.Select(ToString)) + TextRhythmPatternRightDelimiter;

	public RhythmPatternFormat Format(RhythmPattern pattern)
	{
		//Taktlänge
		var noteLength = TryCalculateBarLength(pattern) ?? NoteValue.Eighth;

		var strokes = new StrokeFormat[pattern.Count];
		var nextLength = 1;
		for (var i = pattern.Count - 1; i >= 0; i--)
		{
			var stroke = pattern[i];
			var strokeString = StrokeTypes[(int)stroke.Type];
			if (stroke.Type is StrokeType.Hold or StrokeType.None)
			{
				strokes[i] = new(strokeString, stroke.Type);
				nextLength++;
				continue;
			}

			strokes[i] = new(strokeString, stroke.Type)
			{
				Length = nextLength,
				NoteLength = NoteLength.Create(noteLength, nextLength),
			};
			nextLength = 1;
		}

		return new(RhythmPatternLeftDelimiter, RhythmPatternRightDelimiter, strokes);
	}

	private NoteValue? TryCalculateBarLength(RhythmPattern pattern)
	{
		if (pattern.Count <= 1)
			return NoteValue.Whole;
		if (pattern.Count == 2)
			return NoteValue.Half;

		var noteValue = NoteValue.Whole;
		for (var value = pattern.Count - 1; value != 0; value >>= 1)
			noteValue += 1;

		return noteValue;
	}

	public string ToString(TabNote note, int width)
		=> Format(note, width).ToString();

	public TabNoteFormat Format(TabNote note, int width)
	{
		if (CondenseTabNotes)
		{
			//var prefix = string.Join(string.Empty, note.Modifier.GetFlagsDisplayName());
			var noteText = note.ToString();

			var suffix = noteText[1..];
			if (suffix.Length == 0)
				suffix = null;
			noteText = noteText[0].ToString();
			return new(noteText, width, Suffix: suffix);
		}

		var text = note.ToString();
		var padding = width - text.Length;
		if (padding > 0)
		{
			var paddingLeft = padding / 2;
			var paddingRight = padding - paddingLeft;
			text = new string(' ', paddingLeft) + text + new string(' ', paddingRight);
		}

		return new(text, width);
	}

	public string ToString(Note tuning, int width) => ToString(tuning, width, true);
	private string ToString(Note tuning, int width, bool transform)
		=> Format(tuning, width, transform).ToString();

	public TabNoteFormat Format(Note tuning, int width) => Format(tuning, width, true);
	private TabNoteFormat Format(Note tuning, int width, bool transform)
	{
		var text = ToString(tuning);

		if (CondenseTabNotes)
		{
			if (text.Length <= 1)
				return new(text, width);

			var suffix = text[1..];
			text = text[0].ToString();
			return new(text, width, Suffix: suffix);
		}

		var padding = width - text.Length;
		if (padding > 0)
			text = text + new string(' ', padding);

		return new(text, width);
	}


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

	public int TryReadNote(ReadOnlySpan<char> s, out Note note) => TryReadNote(s, out note, true);
	private int TryReadNote(ReadOnlySpan<char> s, out Note note, bool transform)
	{
		var length = Note.TryRead(s, out note);
		if (length <= 0)
			return 0;

		if (note.Type == NoteType.B)
			switch (GermanMode)
			{
				case GermanNoteMode.German:
					if (note.Accidental == AccidentalType.None && s.StartsWith("B", StringComparison.OrdinalIgnoreCase))
						note = Note.BFlat;
					break;
			}

		if (transform && Transformation is not null)
			note = Transformation.UntransformNote(note);

		return length;
	}

	public int TryReadAccidental(ReadOnlySpan<char> s, out AccidentalType accidental) => TryReadAccidental(s, out accidental, true);
	private int TryReadAccidental(ReadOnlySpan<char> s, out AccidentalType accidental, bool transform)
		=> EnumNameAttribute.TryRead(s, out accidental);

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

	public int TryReadRhythm(ReadOnlySpan<char> s, out RhythmPattern? rhythm)
		=> RhythmPattern.TryRead(s, out rhythm);

	public int TryReadTabNoteModifier(ReadOnlySpan<char> s, out TabNoteModifier modifier) => TryReadTabNoteModifier(s, out modifier, true);
	private int TryReadTabNoteModifier(ReadOnlySpan<char> s, out TabNoteModifier modifier, bool transform)
		=> EnumNameAttribute.TryRead(s, out modifier);
}

public static class SheetFormatterExtensions
{
	public static string ToString(this ChordQuality quality, ISheetFormatter? formatter)
		=> formatter?.ToString(quality) ?? quality.GetDisplayName();

	public static string ToString(this ChordAlterationType type, ISheetFormatter? formatter)
		=> formatter?.ToString(type) ?? type.GetDisplayName();
}
