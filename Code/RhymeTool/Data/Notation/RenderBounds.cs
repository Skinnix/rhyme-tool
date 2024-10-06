namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct RenderBounds(int StartOffset, int AfterOffset)
{
	public static readonly RenderBounds Empty = new(0, 0);

	public int Length => AfterOffset - StartOffset;

	public ContentOffset GetContentOffset(int displayOffset)
		=> new(displayOffset - StartOffset);
}
