using System.Collections;

namespace Skinnix.RhymeTool.Data.Notation;

public readonly record struct RenderBounds(int StartOffset, int AfterOffset)
{
	public static readonly IComparer<RenderBounds> PositionInsideComparer = new InsideComparer();

	public static readonly RenderBounds Empty = new(0, 0);

	public int Length => AfterOffset - StartOffset;

	public ContentOffset GetContentOffset(int displayOffset)
		=> new(displayOffset - StartOffset);

	private class InsideComparer : IComparer<RenderBounds>
	{
		public int Compare(RenderBounds x, RenderBounds y)
		{
			if (x.StartOffset > y.StartOffset)
				return 1;

			if (x.AfterOffset < y.AfterOffset)
				return -1;

			return 0;
		}
	}
}
