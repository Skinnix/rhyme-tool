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

public interface ISheetFormatter
{
	string ToString(Note note);
	Note.NoteFormat Format(Note note);

	string ToString(Chord chord);
	Chord.ChordFormat Format(Chord chord);

	string ToString(ChordAlteration alteration, int index);
	ChordAlteration.AlterationFormat Format(ChordAlteration alteration, int index);

	string ToString(ChordQuality quality);
	string ToString(ChordAlterationType type);

	string ToString(Fingering fingering);
	Fingering.FingeringFormat Format(Fingering fingering);

	string ToString(Stroke stroke);
	Stroke.StrokeFormat Format(Stroke stroke);
	
	string ToString(RhythmPattern.Bar bar);
	RhythmPattern.Bar.BarFormat Format(RhythmPattern.Bar bar);

	string ToString(RhythmPattern pattern);
	RhythmPattern.RhythmPatternFormat Format(RhythmPattern pattern);

	string ToString(NoteLength noteLength);

	string ToString(TabNote note);
	
	string ToString(TabNote note, TabColumnWidth width);
	TabNote.TabNoteFormat Format(TabNote note, TabColumnWidth width);
	TabNote.TabNoteFormat[] FormatAll(IEnumerable<TabNote> notes);

	string ToString(Note tuning, TabColumnWidth width);
	TabNote.TabNoteTuningFormat Format(Note tuning, TabColumnWidth width);
	TabNote.TabNoteTuningFormat[] FormatAll(IEnumerable<Note> tunings);
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
	int TryReadTabNote(ReadOnlySpan<char> s, out TabNote note, int maxNumberLength = 2);
}

public enum GermanNoteMode
{
	[EnumName("B, B♭")]
	AlwaysB,

	[EnumName("H, B")]
	German,

	[EnumName("H, B♭")]
	Descriptive,

	[EnumName("H, H♭")]
	AlwaysH,

	[EnumName("B♮, H♭")]
	ExplicitB,

	[EnumName("H♮, H♭")]
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
	public string? MajorSeventhDegreeModifier { get; init; } = "Δ";

	public string TextDefaultDegreeModifier { get; init; } = string.Empty;
	public string TextSharpDegreeModifier { get; init; } = TextSharpModifier;
	public string TextFlatDegreeModifier { get; init; } = TextFlatModifier;
	public string TextMajorDegreeModifier { get; init; } = "maj";
	public string? TextMajorSeventhDegreeModifier { get; init; } = "maj7";

	public string RhythmPatternLeftDelimiter { get; init; } = "|";
	public string RhythmPatternMiddleDelimiter { get; init; } = "|";
	public string RhythmPatternRightDelimiter { get; init; } = "|";

	public string TextRhythmPatternLeftDelimiter { get; init; } = "|";
	public string TextRhythmPatternMiddleDelimiter { get; init; } = "|";
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

	public Chord.ChordFormat Format(Chord chord) => Format(chord, true);
	protected virtual Chord.ChordFormat Format(Chord chord, bool transform)
	{
		if (transform && Transformation != null)
			chord = Transformation.TransformChord(chord);

		var alterations = chord.Alterations.Select(Format).ToArray();
		return new(chord,
			Format(chord.Root, false),
			ToString(chord.Quality),
			alterations,
			chord.Bass is null ? null : Format(chord.Bass.Value, false));
	}

	public string ToString(Chord chord) => ToString(chord, true);
	protected virtual string ToString(Chord chord, bool transform)
	{
		if (transform && Transformation != null)
			chord = Transformation.TransformChord(chord);

		var sb = new StringBuilder();
		sb.Append(ToString(chord.Root, false));
		sb.Append(ToString(chord.Quality));

		sb.Append(string.Join('/', chord.Alterations.Select(ToString)));

		if (chord.Bass is not null)
			sb.Append('/').Append(ToString(chord.Bass.Value, false));

		return sb.ToString();
	}

	public string ToString(ChordAlteration alteration, int index)
	{
		if (alteration.Degree is (7, ChordDegreeModifier.Major) && MajorSeventhDegreeModifier != null)
			return ToString(alteration.Type) + MajorSeventhDegreeModifier;

		if (alteration.Degree.Modifier == ChordDegreeModifier.None)
			return ToString(alteration.Type) + alteration.Degree.Value.ToString();

		if (index == 0 && alteration.Degree.Modifier is ChordDegreeModifier.Sharp or ChordDegreeModifier.Flat)
			return ToString(alteration.Type) + alteration.Degree.Value + ToString(alteration.Degree.Modifier, false);
		else
			return ToString(alteration.Type) + ToString(alteration.Degree.Modifier, false) + alteration.Degree.Value;
	}

	public string ToString(ChordQuality quality) => quality switch
	{
		ChordQuality.Major => MajorQuality,
		ChordQuality.Minor => MinorQuality,
		ChordQuality.Diminished => DiminishedQuality,
		ChordQuality.Augmented => AugmentedQuality,
		_ => string.Empty
	};

	public virtual string ToString(ChordAlterationType type) => type switch
	{
		ChordAlterationType.Default => DefaultAlteration,
		ChordAlterationType.Addition => AdditionAlteration,
		ChordAlterationType.Suspension => SuspensionAlteration,
		_ => string.Empty
	};

	protected virtual string ToString(ChordDegreeModifier modifier, bool inDocument) => modifier switch
	{
		ChordDegreeModifier.None => inDocument ? DefaultDegreeModifier : TextDefaultDegreeModifier,
		ChordDegreeModifier.Sharp => inDocument ? SharpDegreeModifier : TextSharpDegreeModifier,
		ChordDegreeModifier.Flat => inDocument ? FlatDegreeModifier : TextFlatDegreeModifier,
		ChordDegreeModifier.Major => inDocument ? MajorDegreeModifier : TextMajorDegreeModifier,
		_ => string.Empty
	};

	public virtual ChordAlteration.AlterationFormat Format(ChordAlteration alteration, int index)
	{
		//Sonderfall: maj7
		if (alteration.Type == ChordAlterationType.Default && alteration.Degree is (7, ChordDegreeModifier.Major))
		{
			if (MajorSeventhDegreeModifier != null)
				return new(alteration, MajorSeventhDegreeModifier, string.Empty, string.Empty, false);
			else
				return new(alteration, ToString(alteration.Type), alteration.Degree.Value.ToString(), ToString(alteration.Degree.Modifier, true), false);
		}

		var type = ToString(alteration.Type);
		var modifierAfter = index == 0
			&& alteration.Degree.Modifier is ChordDegreeModifier.Sharp or ChordDegreeModifier.Flat;
		return new(alteration, ToString(alteration.Type), alteration.Degree.Value.ToString(), ToString(alteration.Degree.Modifier, true), modifierAfter);
	}

	protected virtual string ToString(AccidentalType accidental, bool inDocument, bool transform) => accidental switch
	{
		AccidentalType.None => inDocument ? DefaultAccidentalModifier : TextDefaultAccidentalModifier,
		AccidentalType.Sharp => inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier,
		AccidentalType.Flat => inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier,
		_ => string.Empty
	};

	public string ToString(Note note) => ToString(note, true);
	protected virtual string ToString(Note note, bool transform) => Format(note, false, transform).ToString();
	public Note.NoteFormat Format(Note note) => Format(note, true);
	protected virtual Note.NoteFormat Format(Note note, bool transform) => Format(note, true, transform);
	protected virtual Note.NoteFormat Format(Note note, bool inDocument, bool transform)
	{
		if (transform && Transformation != null)
			note = Transformation.TransformNote(note);

		//Germanize
		if (note.Type == NoteType.B)
			switch (GermanMode)
			{
				case GermanNoteMode.AlwaysH:
					return new(note, "H", ToString(note.Accidental, inDocument, transform));
				case GermanNoteMode.German:
					if (note.Accidental == AccidentalType.Flat)
						return new(note, "B", null);
					else
						return new(note, "H", ToString(note.Accidental, inDocument, transform));
				case GermanNoteMode.Descriptive:
					if (note.Accidental == AccidentalType.Flat)
						return new(note, "B", ToString(note.Accidental, inDocument, transform));
					else
						return new(note, "H", ToString(note.Accidental, inDocument, transform));
				case GermanNoteMode.ExplicitB:
					return note.Accidental switch
					{
						AccidentalType.None => new(note, "B", inDocument ? ExplicitNaturalAccidentalModifier : TextExplicitNaturalAccidentalModifier),
						AccidentalType.Sharp => new(note, "B", inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier),
						AccidentalType.Flat => new(note, "B", inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier),
						_ => throw new NotImplementedException("unknown accidental type"),
					};
				case GermanNoteMode.ExplicitH:
					return note.Accidental switch
					{
						AccidentalType.None => new(note, "H", inDocument ? ExplicitNaturalAccidentalModifier : TextExplicitNaturalAccidentalModifier),
						AccidentalType.Sharp => new(note, "H", inDocument ? SharpAccidentalModifier : TextSharpAccidentalModifier),
						AccidentalType.Flat => new(note, "H", inDocument ? FlatAccidentalModifier : TextFlatAccidentalModifier),
						_ => throw new NotImplementedException("unknown accidental type"),
					};

			}

		return new(note, note.Type.GetDisplayName(), ToString(note.Accidental, inDocument, transform));
	}

	public Fingering.FingeringFormat Format(Fingering fingering)
	{
		var positions = fingering.Positions.Select(p => p.ToString()).ToArray();
		return new(fingering, positions, fingering.Positions.Any(p => p.Fret >= 10), fingering.EndsWithSeparator);
	}

	public string ToString(Fingering fingering)
		=> Format(fingering).ToString();

	public string ToString(NoteLength noteLength)
	{
		var strings = noteLength.IsRest ? RestLengths : NoteLengths;
		var s = noteLength.Values.Select(v => v >= 0 && (int)v <= (strings.Length - 1) ? strings[(int)v] : " ");
		return string.Join(null, s) + new string(NoteLengthDot, noteLength.Dots);
	}

	public string ToString(RhythmPattern pattern)
		=> TextRhythmPatternLeftDelimiter
		+ string.Join(TextRhythmPatternMiddleDelimiter, pattern.Select(ToString))
		+ TextRhythmPatternRightDelimiter;
	public RhythmPattern.RhythmPatternFormat Format(RhythmPattern pattern)
		=> new(pattern,
			RhythmPatternLeftDelimiter, RhythmPatternMiddleDelimiter, RhythmPatternRightDelimiter,
			pattern.Select(Format).ToArray());

	public string ToString(RhythmPattern.Bar bar)
		=> string.Join(null, bar.Select(ToString));
	public RhythmPattern.Bar.BarFormat Format(RhythmPattern.Bar bar)
	{
		//Taktlänge
		var noteLength = TryCalculateBarLength(bar) ?? NoteValue.Eighth;

		var strokes = new Stroke.StrokeFormat[bar.Count];
		var nextLength = 1;
		for (var i = bar.Count - 1; i >= 0; i--)
		{
			var stroke = bar[i];
			var strokeString = StrokeTypes[(int)stroke.Type];
			if (stroke.Type is StrokeType.Hold or StrokeType.None)
			{
				strokes[i] = new(stroke, strokeString);
				nextLength++;
				continue;
			}

			strokes[i] = new(stroke, strokeString)
			{
				Length = nextLength,
				NoteLength = NoteLength.Create(noteLength, nextLength),
			};
			nextLength = 1;
		}

		return new(strokes);
	}

	public string ToString(Stroke stroke)
		=> TextStrokeTypes[(int)stroke.Type];
	public Stroke.StrokeFormat Format(Stroke stroke)
		=> new(stroke, TextStrokeTypes[(int)stroke.Type]);

	protected virtual NoteValue? TryCalculateBarLength(RhythmPattern.Bar bar)
	{
		if (bar.Count <= 1)
			return NoteValue.Whole;
		if (bar.Count == 2)
			return NoteValue.Half;

		var noteValue = NoteValue.Whole;
		for (var value = bar.Count - 1; value != 0; value >>= 1)
			noteValue += 1;

		return noteValue;
	}

	public string ToString(TabNote note) => ToString(note, true);
	protected virtual string ToString(TabNote note, bool transform)
		=> note.IsEmpty ? TabNote.EMPTY_CHAR.ToString()
		: note.Value is null ? string.Join(string.Empty, note.Modifier.GetFlagsDisplayName())
		: note.Modifier == TabNoteModifier.None ? note.Value.Value.ToString()
		: $"{string.Join(string.Empty, note.Modifier.GetFlagsDisplayName())}{note.Value.Value}";

	public string ToString(TabNote note, TabColumnWidth width)
		=> ToString(note, width, true);
	protected virtual string ToString(TabNote note, TabColumnWidth width, bool transform)
		=> Format(note, ToString(note, transform), width).ToString();

	public TabNote.TabNoteFormat Format(TabNote note, TabColumnWidth width) => Format(note, width, true);
	protected virtual TabNote.TabNoteFormat Format(TabNote note, TabColumnWidth width, bool transform)
		=> Format(note, ToString(note, transform), width);
	protected virtual TabNote.TabNoteFormat Format(TabNote note, string noteString, TabColumnWidth width)
	{
		if (CondenseTabNotes)
		{
			var suffix = noteString[1..];
			if (suffix.Length == 0)
				suffix = null;
			noteString = noteString[0].ToString();
			return new(note, noteString, width, Suffix: suffix);
		}

		var padding = width.Max - noteString.Length;
		if (padding > 0)
			noteString = noteString + new string(' ', padding);

		return new(note, noteString, width);
	}

	public TabNote.TabNoteFormat[] FormatAll(IEnumerable<TabNote> notes) => FormatAll(notes, true);
	protected virtual TabNote.TabNoteFormat[] FormatAll(IEnumerable<TabNote> notes, bool transform)
	{
		var noteArray = notes.ToArray();
		var noteStrings = TabColumnWidth.Calculate(noteArray.Select(ToString), out var width);
		return noteArray.Zip(noteStrings).Select(n => Format(n.First, n.Second, width)).ToArray();
	}

	public string ToString(Note tuning, TabColumnWidth width) => ToString(tuning, width, true);
	protected virtual string ToString(Note tuning, TabColumnWidth width, bool transform)
		=> Format(tuning, width, transform).ToString();

	public TabNote.TabNoteTuningFormat Format(Note tuning, TabColumnWidth width) => Format(tuning, width, true);
	protected virtual TabNote.TabNoteTuningFormat Format(Note tuning, TabColumnWidth width, bool transform)
		=> Format(tuning, ToString(tuning, transform), width);

	protected virtual TabNote.TabNoteTuningFormat Format(Note tuning, string tuningString, TabColumnWidth width)
	{
		if (CondenseTabNotes)
		{
			if (tuningString.Length <= 1)
				return new(tuning, tuningString, width);

			var suffix = tuningString[1..];
			tuningString = tuningString[0].ToString();
			return new(tuning, tuningString, width, Suffix: suffix);
		}

		var padding = width.Max - tuningString.Length;
		if (padding > 0)
			tuningString = tuningString + new string(' ', padding);

		return new(tuning, tuningString, width);
	}

	public TabNote.TabNoteTuningFormat[] FormatAll(IEnumerable<Note> tunings)
	{
		var tuningArray = tunings.ToArray();
		var tuningStrings = TabColumnWidth.Calculate(tuningArray.Select(ToString), out var width);
		return tuningArray.Zip(tuningStrings).Select(n => Format(n.First, n.Second, width)).ToArray();
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
	protected virtual int TryReadNote(ReadOnlySpan<char> s, out Note note, bool transform)
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
	protected virtual int TryReadAccidental(ReadOnlySpan<char> s, out AccidentalType accidental, bool transform)
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
	protected virtual int TryReadTabNoteModifier(ReadOnlySpan<char> s, out TabNoteModifier modifier, bool transform)
		=> EnumNameAttribute.TryRead(s, out modifier);

	public int TryReadTabNote(ReadOnlySpan<char> s, out TabNote note, int maxNumberLength = 2) => TryReadTabNote(s, out note, maxNumberLength, true);
	protected virtual int TryReadTabNote(ReadOnlySpan<char> s, out TabNote note, int maxNumberLength, bool transform)
		=> TabNote.TryRead(s, out note, maxNumberLength);
}

public static class SheetFormatterExtensions
{
	public static string ToString(this ChordQuality quality, ISheetFormatter? formatter)
		=> formatter?.ToString(quality) ?? quality.GetDisplayName();

	public static string ToString(this ChordAlterationType type, ISheetFormatter? formatter)
		=> formatter?.ToString(type) ?? type.GetDisplayName();
}
