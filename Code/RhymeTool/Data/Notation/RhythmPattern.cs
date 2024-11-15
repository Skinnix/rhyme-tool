using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation;

public class RhythmPattern : IReadOnlyList<RhythmPattern.Bar>
{
	public const char LEFT_DELIMITER = '|';
	public const char MIDDLE_DELIMITER = '|';
	public const char RIGHT_DELIMITER = '|';

	private readonly List<Bar> bars;

	public int Count => bars.Count;

	public Bar this[int index] => bars[index];
	public Bar this[Index index] => bars[index];

	private RhythmPattern(List<Bar> bars)
	{
		this.bars = bars;
	}

	public RhythmPattern(IEnumerable<Bar> bars)
	{
		this.bars = new(bars);
	}

	public IEnumerator<Bar> GetEnumerator() => bars.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static int TryRead(ISheetEditorFormatter? formatter, ReadOnlySpan<char> s, out RhythmPattern? rhythm)
		=> formatter?.TryReadRhythm(s, out rhythm)
		?? TryRead(s, out rhythm);

	public static int TryRead(ReadOnlySpan<char> s, out RhythmPattern? rhythm)
	{
		rhythm = null;
		if (s.Length < 2 || s[0] != LEFT_DELIMITER)
			return -1;

		var bars = new List<Bar>();
		var lastBarIndex = -1;
		var strokes = new List<Stroke>();
		for (var i = 1; i < s.Length;)
		{
			if (s[i] == MIDDLE_DELIMITER)
			{
				var bar = new Bar(strokes);
				bars.Add(bar);
				strokes.Clear();
				i++;
				lastBarIndex = i;
				continue;
			}
			else if (s[i] == RIGHT_DELIMITER)
			{
				var bar = new Bar(strokes);
				bars.Add(bar);
				rhythm = new(bars);
				return i + 1;
			}

			var strokeLength = EnumNameAttribute.TryRead<StrokeType>(s[i..], out var strokeType);
			if (strokeLength <= 0)
				break;

			strokes.Add(new(strokeType));
			/*if (strokeType != StrokeType.None)
				nonEmptyStroke = true;*/
			i += strokeLength;
		}

		//Noch keine vollständigen Takte?
		if (bars.Count == 0)
			return -1;

		rhythm = new(bars);
		return lastBarIndex;

		//if (strokes.Count == 0 && nonEmptyStroke)
		//	return -1;

		//rhythm = new(strokes);
		//return s.Length;
	}

	public RhythmPatternFormat Format(ISheetFormatter? formatter = null)
		=> (formatter ?? DefaultSheetFormatter.Instance).Format(this);

	public override string ToString() => ToString(null);
	public string ToString(ISheetFormatter? formatter)
		=> formatter?.ToString(this)
		?? $"{LEFT_DELIMITER}{string.Join(null, bars)}{RIGHT_DELIMITER}";

	public class Bar : IReadOnlyList<Stroke>
	{
		private readonly List<Stroke> strokes;

		public int Count => strokes.Count;

		public Stroke this[int index] => strokes[index];

		private Bar(List<Stroke> strokes)
		{
			this.strokes = strokes;
		}

		public Bar(IEnumerable<Stroke> strokes)
		{
			this.strokes = new(strokes);
		}

		public IEnumerator<Stroke> GetEnumerator() => strokes.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public BarFormat Format(ISheetFormatter? formatter = null)
			=> (formatter ?? DefaultSheetFormatter.Instance).Format(this);

		public override string ToString() => ToString(null);
		public string ToString(ISheetFormatter? formatter)
			=> string.Join(null, strokes);

		public readonly record struct BarFormat(Stroke.StrokeFormat[] Strokes)
		{
			public override string ToString() => string.Join(null, Strokes);
		}
	}

	public readonly record struct RhythmPatternFormat(RhythmPattern Pattern,
		string LeftDelimiter, string MiddleDelimiter, string RightDelimiter,
		Bar.BarFormat[] Bars)
	{
		public override string ToString() => LeftDelimiter + string.Join(MiddleDelimiter, Bars) + RightDelimiter;
	}
}

