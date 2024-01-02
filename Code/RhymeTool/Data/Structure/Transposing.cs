using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Structure;

public static class Transposing
{
	private static readonly int[] noteIndices = [
		0, //C
		2, //D
		4, //E
		5, //F
		7, //G
		9, //A
		11, //B
	];

	private static readonly Note[] indexNotes = [
		new(NoteType.C, AccidentalType.None),
		new(NoteType.C, AccidentalType.Sharp),
		new(NoteType.D, AccidentalType.None),
		new(NoteType.D, AccidentalType.Sharp),
		new(NoteType.E, AccidentalType.None),
		new(NoteType.F, AccidentalType.None),
		new(NoteType.F, AccidentalType.Sharp),
		new(NoteType.G, AccidentalType.None),
		new(NoteType.G, AccidentalType.Sharp),
		new(NoteType.A, AccidentalType.None),
		new(NoteType.A, AccidentalType.Sharp),
		new(NoteType.B, AccidentalType.None)
	];

	public static Note Transpose(this Note note, int transpose)
	{
		if (transpose == 0)
			return note;

		var noteIndex = GetNoteIndex(note);
		var newIndex = noteIndex + transpose;
		if (newIndex < 0)
			newIndex += 12;
		else if (newIndex >= 12)
			newIndex -= 12;

		return GetNoteFromIndex(newIndex);
	}

	public static Chord Transpose(this Chord chord, int transpose)
	{
		if (transpose == 0)
			return chord;

		var newRoot = chord.Root.Transpose(transpose);
		var newBass = chord.Bass?.Transpose(transpose);

		return chord with
		{
			Root = newRoot,
			Bass = newBass
		};
	}

	private static int GetNoteIndex(Note note)
	{
		var baseIndex = noteIndices[(int)note.Type];
		switch (note.Accidental)
		{
			case AccidentalType.None:
				return baseIndex;
			case AccidentalType.Sharp:
				return baseIndex + 1;
			case AccidentalType.Flat:
				return baseIndex - 1;
			default:
				throw new ArgumentOutOfRangeException(nameof(note.Accidental));
		}
	}

	private static Note GetNoteFromIndex(int index)
		=> indexNotes[index];
}
