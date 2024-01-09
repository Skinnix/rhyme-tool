using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Data.Musical;

public static class ToneConversion
{
	public static Tone AsTone(this Note note)
	{
		var tone = note.Type switch
		{
			NoteType.C => Tone.C,
			NoteType.D => Tone.D,
			NoteType.E => Tone.E,
			NoteType.F => Tone.F,
			NoteType.G => Tone.G,
			NoteType.A => Tone.A,
			NoteType.B => Tone.B,
			_ => throw new ArgumentException("Invalid note type", nameof(note))
		};
		var toneValue = (int)tone;

		if (note.Accidental == AccidentalType.Sharp)
			toneValue++;
		else if (note.Accidental == AccidentalType.Flat)
			toneValue--;

		toneValue %= 12;
		if (toneValue < 0)
			toneValue += 12;

		return (Tone)toneValue;
	}

	public static Note AsNote(this Tone tone, AccidentalType preferredAccidental = AccidentalType.Sharp)
		=> tone switch
		{
			Tone.C => Note.C,
			Tone.D => Note.D,
			Tone.E => Note.E,
			Tone.F => Note.F,
			Tone.G => Note.G,
			Tone.A => Note.A,
			Tone.B => Note.B,
			Tone.CSharp => preferredAccidental == AccidentalType.Flat
				? Note.DFlat
				: Note.CSharp,
			Tone.DSharp => preferredAccidental == AccidentalType.Flat
				? Note.EFlat
				: Note.DSharp,
			Tone.FSharp => preferredAccidental == AccidentalType.Flat
				? Note.GFlat
				: Note.FSharp,
			Tone.GSharp => preferredAccidental == AccidentalType.Flat
				? Note.AFlat
				: Note.GSharp,
			Tone.ASharp => preferredAccidental == AccidentalType.Flat
				? Note.BFlat
				: Note.ASharp,
			_ => throw new ArgumentException("Invalid tone", nameof(tone))
		};
}