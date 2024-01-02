namespace Skinnix.RhymeTool.Data.Structure;

public class SheetNodeChangedEventArgs : EventArgs
{
	public SheetNode Node { get; }

	public SheetNodeChangedEventArgs(SheetNode node)
	{
		Node = node;
	}
}
