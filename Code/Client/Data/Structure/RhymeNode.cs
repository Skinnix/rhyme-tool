namespace Skinnix.RhymeTool.Client.Data.Structure
{
	public abstract class RhymeNode
	{
		public event EventHandler<RhymeNodeChangedEventArgs>? Changed;

		public Guid Id { get; } = Guid.NewGuid();

		protected void InvokeChanged(RhymeNodeChangedEventArgs e)
			=> Changed?.Invoke(this, e);
	}

	public abstract class AtomicRhymeNode : RhymeNode
	{

	}

	public abstract class CompositeRhymeNode : RhymeNode
	{
		public RhymeNodeCollection Nodes { get; } = new();
	}

	public class TextRhymeNode : AtomicRhymeNode
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

		public TextRhymeNode() { }
		
		public TextRhymeNode(string text)
		{
			this.text = text;
		}
	}
}