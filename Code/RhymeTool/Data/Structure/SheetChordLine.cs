using Skinnix.RhymeTool.Data.Structure.Display;

namespace Skinnix.RhymeTool.Data.Structure;

public class SheetChordLine : SheetLine
{
	public List<PositionedChord> Chords { get; } = new();

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null)
	{
		var builder = new SheetDisplayChordLine.Builder();
		foreach (var chord in Chords)
		{
			//Berechne Mindestabstand
			var displayChord = new SheetDisplayChord(chord.Chord);
			var minSpace = formatter?.SpaceBefore(this, builder, displayChord) ?? 0;

			//Verlängere ggf. die Zeile
			builder.ExtendLength(chord.Offset, minSpace);

			//Hänge den Akkord an
			builder.Append(displayChord, formatter);
			
			//Wenn der Akkord ein Suffix hat, hänge es an
			if (chord.Suffix != null)
				builder.Append(new SheetDisplayText(chord.Suffix), formatter);
		}

		return new[]
		{
			builder.CreateDisplayLine()
		};
	}

	public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null)
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

public sealed class PositionedChord
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