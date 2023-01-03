namespace Skinnix.RhymeTool.Client.Data.Structure
{
	public class RhymeNodeChangedEventArgs : EventArgs
	{
		public RhymeNode Node { get; }

		public RhymeNodeChangedEventArgs(RhymeNode node)
		{
			Node = node;
		}
	}
}
