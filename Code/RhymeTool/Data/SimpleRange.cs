using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data;

[Flags]
public enum RangeOverlap
{
	StartFlag = 1,
	EndFlag = 2,
	OverlapFlag = 4,

	InsideRange = 0,

	OutsideBeforeStart = StartFlag,
	OutsideAfterEnd = EndFlag,

	OverlapsStart = OverlapFlag | StartFlag,
	OverlapsEnd = OverlapFlag | EndFlag,
	OverlapsRange = OverlapFlag | StartFlag | EndFlag,
}

public readonly struct SimpleRange
{
	public static readonly SimpleRange CursorAtStart = new(0, 0);
	public static readonly SimpleRange CursorAtEnd = new(-1, -1);

	public int Start { get; }
	public int End { get; }

	public int Length => End - Start;

	public SimpleRange(int start, int end)
	{
		if (start > end)
		{
			Start = end;
			End = start;
		}
		else
		{
			Start = start;
			End = end;
		}
	}

	public static SimpleRange CursorAt(int offset) => new(offset, offset);

	public static SimpleRange AllFromStart(int start) => new(start, int.MaxValue);
	public static SimpleRange AllToEnd(int end) => new(0, end);

	public RangeOverlap CheckOverlap(int offset, int length)
	{
		if (offset > End) return RangeOverlap.OutsideAfterEnd;
		if (offset + length <= Start) return RangeOverlap.OutsideBeforeStart;

		var overlapsStart = offset < Start;
		var overlapsEnd = offset + length > End;

		if (!overlapsStart && !overlapsEnd)
			return RangeOverlap.InsideRange;

		return RangeOverlap.OverlapFlag
			| (overlapsStart ? RangeOverlap.StartFlag : default)
			| (overlapsEnd ? RangeOverlap.EndFlag : default);
	}
}


public readonly record struct ComponentRange
{
	public int? StartComponent { get; }
	public int StartOffset { get; }

	public int? EndComponent { get; }
	public int EndOffset { get; }
}