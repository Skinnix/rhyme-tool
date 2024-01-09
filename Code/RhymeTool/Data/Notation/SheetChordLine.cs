//using System.Collections.ObjectModel;
//using System.Xml.Linq;
//using Skinnix.RhymeTool.ComponentModel;
//using Skinnix.RhymeTool.Data.Notation.Display;

//namespace Skinnix.RhymeTool.Data.Notation;

//[Obsolete]
//public class SheetChordLine : SheetLine, ISheetDisplayLineEditing
//{
//    public ModifiableObservableCollection<PositionedChord> Chords { get; } = new();

//	public SheetChordLine()
//	{
//		Register(Chords);
//	}

//    public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null)
//    {
//        var builder = new SheetDisplayChordLine.Builder();
//        foreach (var chord in Chords)
//        {
//            //Berechne Mindestabstand
//            var displayChord = new SheetDisplayLineChord(chord.Chord);
//            var minSpace = formatter?.SpaceBefore(this, builder, displayChord) ?? 0;

//            //Verlängere ggf. die Zeile
//            builder.ExtendLength(chord.Offset, minSpace);

//            //Hänge den Akkord an
//            builder.Append(displayChord, formatter);

//            //Wenn der Akkord ein Suffix hat, hänge es an
//            if (chord.Suffix != null)
//                builder.Append(new SheetDisplayLineText(chord.Suffix), formatter);
//        }

//        return new[]
//        {
//            builder.CreateDisplayLine(this)
//        };
//    }

//   // public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null)
//   // {
//   //     var offset = 0;
//   //     foreach (var chord in Chords)
//   //     {
//   //         if (offset < chord.Offset)
//			//{
//			//	yield return new SheetDisplaySpacerBlock(chord.Offset - offset);
//			//}

//   //         if (offset < chord.Offset)
//   //             offset = chord.Offset;

//   //         yield return new SheetDisplayContentBlock(new SheetDisplayChordLine(new SheetDisplayLineChord(chord.Chord)) { Editing = this });
//   //         offset += chord.Chord.ToString().Length;

//   //         if (chord.Suffix != null)
//   //         {
//   //             yield return new SheetDisplayContentBlock(new SheetDisplayTextLine(new SheetDisplayLineText(chord.Suffix)) { Editing = this });
//   //             offset += chord.Suffix.Length;
//   //         }
//   //     }
//   // }

//	public bool InsertContent(string content, int selectionStart, int selectionEnd) => throw new NotImplementedException();
//	public bool DeleteContent(int selectionStart, int selectionEnd, bool forward = false) => throw new NotImplementedException();
//}

//public sealed class PositionedChord : DeepObservableBase
//{
//	private Chord chord;
//	public Chord Chord
//	{
//		get => chord;
//		set => Set(ref chord, value);
//	}

//	private int offset;
//	public int Offset
//	{
//		get => offset;
//		set => Set(ref offset, value);
//	}

//	private string? suffix;
//	public string? Suffix
//	{
//		get => suffix;
//		set => Set(ref suffix, value);
//	}

//    public PositionedChord(Chord chord, int offset)
//    {
//        this.chord = chord;
//        this.offset = offset;
//    }
//}