namespace Skinnix.RhymeTool.Data.Structure;

public abstract class SheetNode
{
	public event EventHandler<SheetNodeChangedEventArgs>? Changed;

	public Guid Id { get; init; } = Guid.NewGuid();

	protected void InvokeChanged(SheetNodeChangedEventArgs e)
		=> Changed?.Invoke(this, e);
}

public abstract class AtomicSheetNode : SheetNode
{

}

public abstract class CompositeSheetNode : SheetNode
{
	public SheetNodeCollection Nodes { get; } = new();
}

public class TextSheetNode : AtomicSheetNode
{
	private string text = string.Empty;

	public string Text
	{
		get => text;
		set
		{
			if (text == value)
				return;

			text = value;
			InvokeChanged(new(this));
		}
	}

	public TextSheetNode() { }
	
	public TextSheetNode(string text)
	{
		this.text = text;
	}
}