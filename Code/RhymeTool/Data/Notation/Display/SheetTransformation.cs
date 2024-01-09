using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public record SheetTransformation
{
    private int transpose;
    public int Transpose
    {
        get => transpose;
        init
        {
            transpose = value % 12;
            if (transpose <= -6)
                transpose += 12;
            else if (transpose > 6)
                transpose -= 12;
        }
    }

    public Chord TransformChord(Chord chord)
    {
        if (Transpose == 0)
            return chord;

        return chord.Transpose(Transpose);
    }

    public Note TransformNote(Note note)
    {
        if (Transpose == 0)
            return note;

        return note.Transpose(Transpose);
    }
}
