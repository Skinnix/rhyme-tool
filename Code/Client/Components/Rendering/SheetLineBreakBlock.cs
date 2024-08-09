using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components.Rendering;

public sealed record SheetLineBreakBlock(IReadOnlyCollection<SheetLineBreakBlock.BlockLine> Lines)
{
	public static IEnumerable<SheetLineBreakBlock> Create(IReadOnlyCollection<SheetDisplayLine> lines, int maxLength)
	{
		var builder = new Builder(lines);
		var enumerators = lines.Select(l => l.GetElements().GetEnumerator()).ToArray();
		for(; ; )
		{
			int i = 0;
			int breakPointsFound = 0;
			foreach (var enumerator in enumerators)
			{
				while (enumerator.MoveNext())
				{
					var element = enumerator.Current;
					if (element is SheetDisplayLineBreakPoint breakPoint)
					{
						builder.AddBreakPoint(i, breakPoint);
						breakPointsFound++;
						break;
					}

					builder.AddElement(i, element);
				}
				i++;
			}

			if (breakPointsFound == 0)
			{
				if (!builder.IsEmpty)
				{
					builder.CommitPending();
					yield return builder.Build();
				}

				break;
			}

			if (builder.CurrentLength > maxLength)
			{
				yield return builder.Build();
				continue;
			}

			builder.CommitPending();
		}
	}

	public sealed record BlockLine(SheetDisplayLine DisplayLine)
	{
		public class Builder(SheetDisplayLine displayLine)
		{
			private readonly List<List<SheetDisplayLineElement>> elements = new();
			private List<SheetDisplayLineElement> pending = new();
			private SheetDisplayLineBreakPoint? currentBreakPoint;
			private SheetDisplayLineBreakPoint? nextBreakPoint;

			public SheetDisplayLine DisplayLine { get; } = displayLine;

			public bool IsEmpty => elements.Count == 0 && pending.Count == 0;

			public int CurrentLength
			{
				get
				{
					var first = elements.FirstOrDefault()?.FirstOrDefault()
						?? pending.FirstOrDefault();
					var last = pending.LastOrDefault()
						?? elements.LastOrDefault()?.LastOrDefault();
					if (first is null || last is null)
						return 0;

					return last.DisplayOffset + last.DisplayLength - first.DisplayOffset;
				}
			}

			public void AddPending(SheetDisplayLineElement element)
			{
				pending.Add(element);
			}

			public void AddPending(IEnumerable<SheetDisplayLineElement> elements)
			{
				pending.AddRange(elements);
			}

			public void AddBreakPoint(SheetDisplayLineBreakPoint breakPoint)
			{
				nextBreakPoint = breakPoint;
			}

			public void CommitPending()
			{
				elements.Add(pending);
				pending = new();
				currentBreakPoint = nextBreakPoint;
				nextBreakPoint = null;
			}

			public BlockLine Build()
			{
				var result = new BlockLine(DisplayLine.WithElements(elements.SelectMany(e => e)));

				elements.Clear();
				if (currentBreakPoint?.Offset > 0)
				{
					pending.Insert(0, new SheetDisplayLineFormatSpace(currentBreakPoint.Offset));
				}

				return result;
			}
		}
	}

	public class Builder(IReadOnlyCollection<SheetDisplayLine> lines)
	{
		private readonly BlockLine.Builder[] lineBuilders = lines.Select(l => new BlockLine.Builder(l)).ToArray();

		public bool IsEmpty => lineBuilders.All(l => l.IsEmpty);

		public int CurrentLength => lineBuilders.Max(l => l.CurrentLength);

		public void AddElement(int lineIndex, SheetDisplayLineElement element)
		{
			lineBuilders[lineIndex].AddPending(element);
		}

		public void AddElements(int lineIndex, IEnumerable<SheetDisplayLineElement> elements)
		{
			lineBuilders[lineIndex].AddPending(elements);
		}

		public void AddBreakPoint(int lineIndex, SheetDisplayLineBreakPoint breakPoint)
		{
			lineBuilders[lineIndex].AddBreakPoint(breakPoint);
		}

		public void CommitPending()
		{
			foreach (var lineBuilder in lineBuilders)
			{
				lineBuilder.CommitPending();
			}
		}

		public SheetLineBreakBlock Build()
		{
			return new SheetLineBreakBlock(lineBuilders.Select(l => l.Build()).ToArray());
		}
	}
}
