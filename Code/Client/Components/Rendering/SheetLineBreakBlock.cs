using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components.Rendering;

public sealed record SheetBlock(IReadOnlyList<SheetBlock.SheetBlockLine> Lines)
{
	public bool IsEmpty => Lines.All(l => l.IsEmpty);

	public static IEnumerable<SheetBlock> Create(IReadOnlyList<SheetDisplayLine> lines, IEnumerable<int> maxLengthPerLine)
	{
		//Lese alle Zeilenelemente
		var lineElements = lines.Select(l => l.GetElements().ToArray()).ToArray();

		//Länge der ersten Zeile
		var maxLengthPerLineEnumerator = maxLengthPerLine.GetEnumerator();
		int currentLineMaxLength;
		if (maxLengthPerLineEnumerator.MoveNext())
			currentLineMaxLength = maxLengthPerLineEnumerator.Current;
		else
			currentLineMaxLength = int.MaxValue;

		//Sind die Zeilen sowieso kürzer?
		if (lineElements.All(e =>
		{
			if (e.Length == 0) return true;

			var last = e[^1];
			return last.DisplayOffset + last.DisplayLength <= currentLineMaxLength;
		}))
		{
			//Nur ein Block, der alle Elemente enthält
			yield return CreateBlock(lines, lineElements, null, null, new int[lineElements.Length]);
			yield break;
		}

		//Finde zuerst alle Breakpoints
		var breakpoints = lineElements.Select(l => FindBreakpoints(l).ToArray()).ToArray();

		//Stelle die Blöcke zusammen
		var currentBlockStartOffset = new int[breakpoints.Length];
		BreakPointInLine[]? lastBreakingGroup = null;
		BreakPointInLine[]? lastGroup = null;
		BreakPointInLine[]? lastNonSpaceBeforeGroup = null;
		foreach (var group in GroupBreakpoints(breakpoints))
		{
			//Wurden nur Leerzeichen hinzugefügt?
			if (group.All(b => b.OnlySpacesBefore))
			{
				lastGroup = group;
				continue; //Leerzeichen am Ende der Zeile würden sowieso ignoriert
			}

			//Erster Block?
			if (lastNonSpaceBeforeGroup is null)
			{
				lastNonSpaceBeforeGroup = group;
				lastGroup = group;
				continue;
			}

			//Berechne aktuelle Blocklänge (ignoriere aus dem aktuellen Block die Zeilen, die nur Leerzeichen enthalten)
			var currentOffsets = currentBlockStartOffset
				.Zip(group.Select(g => g.DisplayOffset))
				.Select(pair => pair.First + pair.Second);
			var currentLengths = lastBreakingGroup is null ? currentOffsets
				: currentOffsets.Zip(lastBreakingGroup)
				.Select(pair => pair.First - pair.Second.DisplayOffset);
			var currentLength = currentLengths.Zip(group).Max(g => g.Second.OnlySpacesBefore ? 0 : g.First);

			//Ist der aktuelle Block zu lang?
			if (currentLength > currentLineMaxLength)
			{
				//Sonderfall: ist es der erste Block?
				if (lastNonSpaceBeforeGroup is null)
				{
					//Verwende die aktuellen Breakpoints, auch wenn der Block dann zu lang ist
					yield return CreateBlock(lines, lineElements, null, group, currentBlockStartOffset);

					//Lese nächste Zeilenlänge
					if (maxLengthPerLineEnumerator?.MoveNext() == true)
						currentLineMaxLength = maxLengthPerLineEnumerator.Current;
					else
						maxLengthPerLineEnumerator = null;

					//Beginne den neuen Block
					lastNonSpaceBeforeGroup = group;
					lastBreakingGroup = group;
					currentBlockStartOffset = lastBreakingGroup.Select(g => g.StartingPointOffset).ToArray();
					continue;
				}

				//Verwende die letzten Breakpoints, ignoriere Gruppen, die nur Leerzeichen enthalten
				yield return CreateBlock(lines, lineElements, lastBreakingGroup, lastNonSpaceBeforeGroup, currentBlockStartOffset);

				//Lese nächste Zeilenlänge
				if (maxLengthPerLineEnumerator?.MoveNext() == true)
					currentLineMaxLength = maxLengthPerLineEnumerator.Current;
				else
					maxLengthPerLineEnumerator = null;

				//Starte den nächsten Block bei der vorherigen Gruppe
				lastBreakingGroup = lastGroup ?? lastNonSpaceBeforeGroup;

				//Beginne den neuen Block
				currentBlockStartOffset = lastBreakingGroup.Select(g => g.StartingPointOffset).ToArray();

				//Berechne aktuelle Blocklänge
				currentLength = currentBlockStartOffset
					.Zip(lastBreakingGroup.Select(g => g.DisplayOffset))
					.Max(pair => pair.First + pair.Second);
			}

			//Gehe zum nächsten Block über
			lastNonSpaceBeforeGroup = group;
			lastGroup = group;
		}

		//Muss der letzte Block nochmal geteilt werden?
		if (lastGroup is not null && lastNonSpaceBeforeGroup is not null)
		{
			//Berechne aktuelle Blocklänge
			var currentOffsets = currentBlockStartOffset
				.Zip(lastNonSpaceBeforeGroup.Select(g => g.DisplayOffset))
				.Select(pair => pair.First + pair.Second);
			var currentLengths = lastBreakingGroup is null ? currentOffsets
				: currentOffsets.Zip(lastBreakingGroup)
				.Select(pair => pair.First - pair.Second.DisplayOffset);
			var currentLength = currentLengths.Zip(lastGroup).Max(g => g.Second.OnlySpacesBefore ? 0 : g.First);

			if (currentLength > currentLineMaxLength)
			{
				//Verwende die letzten Breakpoints, ignoriere Gruppen, die nur Leerzeichen enthalten
				yield return CreateBlock(lines, lineElements, lastBreakingGroup, lastNonSpaceBeforeGroup, currentBlockStartOffset);

				//Starte den nächsten Block bei der vorherigen Gruppe
				lastBreakingGroup = lastGroup ?? lastNonSpaceBeforeGroup;
			}
		}

		//Erstelle den letzten Block
		var lastBlock = CreateBlock(lines, lineElements, lastBreakingGroup, null, currentBlockStartOffset);
		if (!lastBlock.IsEmpty)
			yield return lastBlock;
	}

	private static IEnumerable<BreakPointInLine> FindBreakpoints(IEnumerable<SheetDisplayLineElement> elements)
	{
		int index = 0;
		bool onlySpacesBefore = true;
		int lastBreakpointIndex = -1;
		SheetDisplayLineElement? lastElement = null;
		var endsWithBreakpoint = false;
		foreach (var element in elements)
		{
			endsWithBreakpoint = false;
			if (element is SheetDisplayLineBreakPoint breakpoint)
			{
				yield return new(index, breakpoint.StartingPointOffset, breakpoint.DisplayOffset, onlySpacesBefore);
				lastBreakpointIndex = breakpoint.BreakPointIndex;
				onlySpacesBefore = true;
				endsWithBreakpoint = true;
			}
			else if (!element.IsSpace)
			{
				onlySpacesBefore = false;
			}

			index++;
			lastElement = element;
		}

		if (!endsWithBreakpoint && lastElement is not null)
		{
			//Füge einen Breakpoint am Ende der Zeile hinzu
			yield return new BreakPointInLine(index, 0, lastElement.DisplayOffset + lastElement.DisplayLength, onlySpacesBefore);
		}
	}

	private static IEnumerable<BreakPointInLine[]> GroupBreakpoints(IEnumerable<BreakPointInLine>[] breakpoints)
	{
		var enumerators = breakpoints.Select(b => b.GetEnumerator()).ToArray();
		var finished = new bool[breakpoints.Length];
		for (; ; )
		{
			var current = new BreakPointInLine[breakpoints.Length];
			for (int i = 0; i < breakpoints.Length; i++)
			{
				if (finished[i])
					continue;

				if (!enumerators[i].MoveNext())
				{
					finished[i] = true;
					continue;
				}

				current[i] = enumerators[i].Current;
			}

			if (current.All(c => c == default))
				break;

			yield return current;
		}
	}

	private static SheetBlock CreateBlock(IReadOnlyList<SheetDisplayLine> lines, SheetDisplayLineElement[][] lineElements,
		BreakPointInLine[]? lastBreakingGroup, BreakPointInLine[]? breakingGroup, int[] startOffset)
	{
		//Erzeuge die Zeilen
		var blockLines = new SheetBlockLine[lines.Count];
		for (var i = 0; i < blockLines.Length; i++)
		{
			//Berechne Start- und Endpunkt
			var startIndex = lastBreakingGroup?[i].Index ?? 0;
			var endIndex = breakingGroup?[i].Index - 1;
			blockLines[i] = new SheetBlockLine(lines[i], lineElements[i], startIndex, endIndex, startOffset[i]);
		}

		var block = new SheetBlock(blockLines);
		return block;
	}

	public sealed record SheetBlockLine
	{
		private readonly SheetDisplayLineElement[] elements;
		private readonly SheetDisplayLineFormatSpace? spaceBefore;
		private readonly int startIndex, length;

		public SheetDisplayLine Line { get; }
		internal bool IsEmpty => !GetElements().Any(e => e is not SheetDisplayLineBreakPoint);

		public SheetBlockLine(SheetDisplayLine line, SheetDisplayLineElement[] elements, int startIndex, int? endIndex, int startOffset)
		{
			Line = line;
			this.elements = elements;
			this.startIndex = startIndex;
			this.length = endIndex.HasValue ? endIndex.Value - startIndex + 1 : elements.Length - startIndex;

			if (startOffset > 0)
				spaceBefore = new SheetDisplayLineFormatSpace(startOffset)
				{
					Slice = GetElements().FirstOrDefault()?.Slice is SheetDisplaySliceInfo firstElementSlice
						? new SheetDisplaySliceInfo(firstElementSlice.Component, ContentOffset.Zero, IsVirtual: true)
						: null
				};
		}

		public IEnumerable<SheetDisplayLineElement> GetElements()
		{
			if (spaceBefore is not null)
				yield return spaceBefore;

			for (var i = 0; i < length; i++)
				yield return elements[i + startIndex];
		}
	}

	private record struct BreakPointInLine(int Index, int StartingPointOffset, int DisplayOffset, bool OnlySpacesBefore);
}



//public sealed record SheetLineBreakBlock(IReadOnlyCollection<SheetLineBreakBlock.BlockLine> Lines)
//{
//	public static IEnumerable<SheetLineBreakBlock> Create(IReadOnlyCollection<SheetDisplayLine> lines, int maxLength)
//	{
//		var builder = new Builder(lines);
//		var enumerators = lines.Select(l => l.GetElements().GetEnumerator()).ToArray();
//		for(; ; )
//		{
//			int i = 0;
//			int breakPointsFound = 0;
//			foreach (var enumerator in enumerators)
//			{
//				while (enumerator.MoveNext())
//				{
//					var element = enumerator.Current;
//					if (element is SheetDisplayLineBreakPoint breakPoint)
//					{
//						builder.AddBreakPoint(i, breakPoint);
//						breakPointsFound++;
//						break;
//					}

//					builder.AddElement(i, element);
//				}
//				i++;
//			}

//			if (breakPointsFound == 0)
//			{
//				if (!builder.IsEmpty)
//				{
//					builder.CommitPending();
//					yield return builder.Build();
//				}

//				break;
//			}

//			if (builder.CurrentLength > maxLength)
//			{
//				yield return builder.Build();
//				continue;
//			}

//			builder.CommitPending();
//		}
//	}

//	public sealed record BlockLine(SheetDisplayLine DisplayLine)
//	{
//		public class Builder(SheetDisplayLine displayLine)
//		{
//			private readonly List<List<SheetDisplayLineElement>> elements = new();
//			private List<SheetDisplayLineElement> pending = new();
//			private SheetDisplayLineBreakPoint? currentBreakPoint;
//			private SheetDisplayLineBreakPoint? nextBreakPoint;

//			public SheetDisplayLine DisplayLine { get; } = displayLine;

//			public bool IsEmpty => elements.Count == 0 && pending.Count == 0;

//			public int CurrentLength
//			{
//				get
//				{
//					var first = elements.FirstOrDefault()?.FirstOrDefault()
//						?? pending.FirstOrDefault();
//					var last = pending.LastOrDefault()
//						?? elements.LastOrDefault()?.LastOrDefault();
//					if (first is null || last is null)
//						return 0;

//					return last.DisplayOffset + last.DisplayLength - first.DisplayOffset;
//				}
//			}

//			public void AddPending(SheetDisplayLineElement element)
//			{
//				pending.Add(element);
//			}

//			public void AddPending(IEnumerable<SheetDisplayLineElement> elements)
//			{
//				pending.AddRange(elements);
//			}

//			public void AddBreakPoint(SheetDisplayLineBreakPoint breakPoint)
//			{
//				nextBreakPoint = breakPoint;
//			}

//			public void CommitPending()
//			{
//				elements.Add(pending);
//				pending = new();
//				currentBreakPoint = nextBreakPoint;
//				nextBreakPoint = null;
//			}

//			public BlockLine Build()
//			{
//				var result = new BlockLine(DisplayLine.WithElements(elements.SelectMany(e => e)));

//				elements.Clear();
//				if (currentBreakPoint?.Offset > 0)
//				{
//					pending.Insert(0, new SheetDisplayLineFormatSpace(currentBreakPoint.Offset));
//				}

//				return result;
//			}
//		}
//	}

//	public class Builder(IReadOnlyCollection<SheetDisplayLine> lines)
//	{
//		private readonly BlockLine.Builder[] lineBuilders = lines.Select(l => new BlockLine.Builder(l)).ToArray();

//		public bool IsEmpty => lineBuilders.All(l => l.IsEmpty);

//		public int CurrentLength => lineBuilders.Max(l => l.CurrentLength);

//		public void AddElement(int lineIndex, SheetDisplayLineElement element)
//		{
//			lineBuilders[lineIndex].AddPending(element);
//		}

//		public void AddElements(int lineIndex, IEnumerable<SheetDisplayLineElement> elements)
//		{
//			lineBuilders[lineIndex].AddPending(elements);
//		}

//		public void AddBreakPoint(int lineIndex, SheetDisplayLineBreakPoint breakPoint)
//		{
//			lineBuilders[lineIndex].AddBreakPoint(breakPoint);
//		}

//		public void CommitPending()
//		{
//			foreach (var lineBuilder in lineBuilders)
//			{
//				lineBuilder.CommitPending();
//			}
//		}

//		public SheetLineBreakBlock Build()
//		{
//			return new SheetLineBreakBlock(lineBuilders.Select(l => l.Build()).ToArray());
//		}
//	}
//}
