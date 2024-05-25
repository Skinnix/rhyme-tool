using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class WordComponent : DeepObservableBase, ISheetDisplayLineElementSource, IHasCollectionParent<SheetComplexWord>
{
	public SheetComplexWord? Parent { get; private set; }

	public bool IsEmpty => string.IsNullOrEmpty(Text) && Attachments.Count == 0;
	public bool IsSpace => string.IsNullOrWhiteSpace(Text);

	private string text;
	public string Text
	{
		get => text;
		set => Set(ref text, value);
	}

	public ModifiableObservableCollectionWithParent<WordComponentAttachment, WordComponent> Attachments { get; }

	public int Length => Text.Length;

	public WordComponent(string text)
	{
		this.text = text;
		Attachments = Register(new ModifiableObservableCollectionWithParent<WordComponentAttachment, WordComponent>(this));
	}

	void IHasCollectionParent<SheetComplexWord>.SetParent(SheetComplexWord? parent)
	{
		Parent = parent;
	}

	public bool CutContent(SimpleRange range, ISheetFormatter? formatter)
	{
		//Ist die Auswahl größer als die Komponente?
		if (range.End > Length)
			return false;

		//Von wo kürzen?
		if (range.Start == 0)
		{
			//Kürze die Komponente von links
			Text = Text[range.End..];
		}
		else if (range.End == Length)
		{
			//Kürze die Komponente von rechts
			Text = Text[..range.Start];
		}
		else
		{
			//Kürze die Komponente von beiden Seiten
			Text = Text[..range.Start] + Text[range.End..];
		}

		return true;
	}

	public override string ToString() => Text;
}

public abstract class WordComponentAttachment : SheetLineComponent, IHasCollectionParent<WordComponent>
{
	public WordComponent? Parent { get; private set; }

	private int offset;
	public int Offset
	{
		get => offset;
		set => Set(ref offset, value);
	}

	public abstract object GetAttachment();
	public abstract SheetCompositeLineBlockRow CreateDisplayBlockLine();

	void IHasCollectionParent<WordComponent>.SetParent(WordComponent? parent)
	{
		Parent = parent;
	}

	public override abstract string ToString();
}

public class WordComponentChord : WordComponentAttachment, ISheetDisplayLineElementSource
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

	public override string ToString() => Chord.ToString();
}

public class WordComponentText : WordComponentAttachment, ISheetDisplayLineElementSource
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
			new SheetDisplayLineAnchorText(this, Text));

	public override string ToString() => Text;
}
