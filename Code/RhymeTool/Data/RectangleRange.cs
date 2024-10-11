using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data;

public readonly record struct RectangleRange
{
	public int Start { get; }
	public int End { get; }

	public int StartLine { get; }
	public int EndLine { get; }

	public int LineCount => EndLine - StartLine;

	public RectangleRange(int start, int end, int startLine, int endLine)
	{
		this.Start = start;
		this.End = end;
		this.StartLine = startLine;
		this.EndLine = endLine;
	}
}
