using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class WordComponent : DeepObservableBase
{
	private string text;
	public string Text
	{
		get => text;
		set => Set(ref text, value);
	}

	public ModifiableObservableCollection<WordComponentAttachment> Attachments { get; }

	public int Length => Text.Length;

	public WordComponent(string text)
	{
		this.text = text;
		Attachments = Register(new ModifiableObservableCollection<WordComponentAttachment>());
	}
}

public abstract class WordComponentAttachment : SheetLineComponent
{
	private int offset;
	public int Offset
	{
		get => offset;
		set => Set(ref offset, value);
	}

	public abstract object GetAttachment();
	public abstract SheetCompositeLineBlockRow CreateDisplayBlockLine();
}

public class WordComponentChord : WordComponentAttachment
{
	private Chord chord;
	public Chord Chord
	{
		get => chord;
		set => Set(ref chord, value);
	}

	public WordComponentChord(Chord chord)
	{
		this.chord = chord;
	}

	public override Chord GetAttachment() => Chord;
	public override SheetCompositeLineBlockRow<SheetDisplayChordLine.Builder> CreateDisplayBlockLine()
		=> new SheetCompositeLineBlockRow<SheetDisplayChordLine.Builder>(
			new SheetDisplayLineChord(this, Chord));

	public override SheetLineComponentCutResult CutContent(int cutOffset, int cutLength, ISheetFormatter? formatter) => throw new NotImplementedException();
}

public class WordComponentText : WordComponentAttachment
{
	private string text;
	public string Text
	{
		get => text;
		set => Set(ref text, value);
	}

	public WordComponentText(string text)
	{
		this.text = text;
	}

	public override string GetAttachment() => Text;
	public override SheetCompositeLineBlockRow<SheetDisplayChordLine.Builder> CreateDisplayBlockLine()
		=> new SheetCompositeLineBlockRow<SheetDisplayChordLine.Builder>(
			new SheetDisplayLineText(this, Text));

	public override SheetLineComponentCutResult CutContent(int cutOffset, int cutLength, ISheetFormatter? formatter) => throw new NotImplementedException();
}
