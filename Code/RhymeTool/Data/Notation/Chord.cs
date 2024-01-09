using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;
using Skinnix.RhymeTool;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public enum ChordQuality : byte
{
    [EnumName("", "M")]
    Major,

    [EnumName("m", "min", "-", Blacklist = ["ma"])]
    Minor,

    [EnumName("0", "o", "dim")]
    Diminished,

    [EnumName("+", "aug")]
    Augmented,
}

public sealed record Chord(Note Root, ChordQuality Quality)
{
    public Note? Bass { get; init; }
    public IReadOnlyList<ChordAlteration> Alterations { get; init; } = Array.Empty<ChordAlteration>();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Root);
        sb.Append(Quality.GetDisplayName());

        var firstAlteration = true;
        foreach (var alteration in Alterations)
        {
            if (firstAlteration)
                firstAlteration = false;
            else
                sb.Append('/');

            sb.Append(alteration);
        }

        if (Bass is not null)
            sb.Append($"/{Bass}");

        return sb.ToString();
    }

    public string ToString(ISheetFormatter? formatter)
        => formatter?.Format(this) ?? ToString();

    public static int TryRead(ReadOnlySpan<char> s, out Chord? chord)
    {
        chord = null;
        if (s.IsEmpty)
            return -1;

        //Lese Note
        var noteLength = Note.TryRead(s, out var note);
        if (noteLength == -1)
            return -1;

        //Lese Akkordtyp
        var offset = noteLength;
        var qualityLength = EnumExtensions.TryRead(s[offset..], out ChordQuality quality);
        if (qualityLength != -1)
            offset += qualityLength;

        ////Direkt Bassnote?
        //if (offset < s.Length && s[offset] == '/')
        //{
        //	//Akkord endet auf jeden Fall hier, weil erste Alteration keinen Slash haben kann
        //	var bassNoteLength = Note.TryRead(s[(offset + 1)..], out var bassNoteRead);
        //	if (bassNoteLength == -1)
        //	{
        //		chord = new Chord(note, quality);
        //		return offset;
        //	}
        //	else
        //	{
        //		offset += bassNoteLength + 1;
        //		chord = new Chord(note, quality) { Bass = bassNoteRead };
        //		return offset;
        //	}
        //}

        //Slash als nächstes?
        var slashRead = false;
        if (offset < s.Length && s[offset] == '/')
        {
            slashRead = true;
            offset++;
        }

        //Lese Alterationen
        var alterations = new List<ChordAlteration>();
        Note? bassNote = null;
        while (offset < s.Length)
        {
            //Lese Alteration
            var alterationLength = ChordAlteration.TryRead(s[offset..], out var alteration);
            if (alterationLength != -1 && (!slashRead || alterations.Count > 0))
            {
                slashRead = false;
                alterations.Add(alteration);
                offset += alterationLength;

                //Kein Slash als nächstes?
                if (offset >= s.Length || s[offset] != '/')
                    break;

                //Lese Slash
                slashRead = true;
                offset++;
            }
            else if (slashRead)
            {
                //Lese Bassnote
                slashRead = false;
                var bassNoteLength = Note.TryRead(s[offset..], out var bassNoteRead);
                if (bassNoteLength != -1)
                {
                    offset += bassNoteLength;
                    bassNote = bassNoteRead;
                    break;
                }

                //Keine Bassnote
                break;
            }
            else
            {
                //Weder Alteration noch Bassnote
                break;
            }
        }

        //Wurde am Ende ein Slash gelesen, der nicht mehr dazugehört?
        if (slashRead)
            offset--;

        //Erfolgreich gelesen
        chord = new Chord(note, quality)
        {
            Bass = bassNote,
            Alterations = alterations
        };
        return offset;
    }
}
