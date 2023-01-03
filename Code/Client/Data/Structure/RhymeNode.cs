namespace Skinnix.RhymeTool.Client.Data.Structure
{
	public abstract class RhymeNode
	{
		public event EventHandler<RhymeNodeChangedEventArgs>? Changed;

		protected void InvokeChanged(RhymeNodeChangedEventArgs e)
			=> Changed?.Invoke(this, e);
	}

	public class TextRhymeNode : RhymeNode
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
	}
}