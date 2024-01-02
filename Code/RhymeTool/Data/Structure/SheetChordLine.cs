using Skinnix.RhymeTool.Data.Structure.Display;

namespace Skinnix.RhymeTool.Data.Structure;

public class SheetChordLine : SheetLine
{
	public List<PositionedChord> Chords { get; } = new();

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines()
	{
		var builder = new SheetDisplayChordLine.Builder();
		foreach (var chord in Chords)
		{
			builder.ExtendLength(chord.Offset);
			builder.Append(new SheetDisplayChord(chord.Chord));
			
			if (chord.Suffix != null)
				builder.Append(new SheetDisplayText(chord.Suffix));
		}

		return new[]
		{
			builder.CreateDisplayLine()
		};
	}

	public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks()
	{
		var offset = 0;
		foreach (var chord in Chords)
		{
			if (offset < chord.Offset)
				yield return new SheetDisplaySpacerBlock(chord.Offset - offset);

			if (offset < chord.Offset)
				offset = chord.Offset;

			yield return new SheetDisplayContentBlock(new SheetDisplayChordLine(new SheetDisplayChord(chord.Chord)));
			offset += chord.Chord.ToString().Length;

			if (chord.Suffix != null)
			{
				yield return new SheetDisplayContentBlock(new SheetDisplayTextLine(new SheetDisplayText(chord.Suffix)));
				offset += chord.Suffix.Length;
			}
		}
	}
}

public class PositionedChord
{
	public Chord Chord { get; set; }
	public int Offset { get; set; }

	public string? Suffix { get; set; }

	public PositionedChord(Chord chord, int offset)
	{
		Chord = chord;
		Offset = offset;
	}
}