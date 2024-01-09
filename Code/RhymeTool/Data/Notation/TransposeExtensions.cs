using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Musical;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Data.Notation;

public static class TransposeExtensions
{
    public static Note Transpose(this Note note, int transpose)
    {
        if (transpose == 0)
            return note;

		var tone = note.AsTone();
		tone = tone.Transpose(transpose);
		return tone.AsNote(note.Accidental);
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
}
