using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation;

public class RhythmPattern : IReadOnlyList<Stroke>
{
	public const char LEFT_DELIMITER = '|';
	public const char RIGHT_DELIMITER = '|';

	private readonly List<Stroke> strokes;

	public int Count => strokes.Count;

	public Stroke this[int index] => strokes[index];
	public Stroke this[Index index] => strokes[index];

	private RhythmPattern(List<Stroke> strokes)
	{
		this.strokes = strokes;
	}

	public RhythmPattern(IEnumerable<Stroke> strokes)
	{
		this.strokes = new(strokes);
	}

	public IEnumerator<Stroke> GetEnumerator() => strokes.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static int TryRead(ISheetEditorFormatter? formatter, ReadOnlySpan<char> s, out RhythmPattern? rhythm)
		=> formatter?.TryReadRhythm(s, out rhythm)
		?? TryRead(s, out rhythm);

	public static int TryRead(ReadOnlySpan<char> s, out RhythmPattern? rhythm)
	{
		rhythm = null;
		if (s.Length < 2 || s[0] != LEFT_DELIMITER)
			return -1;

		var strokes = new List<Stroke>();
		//bool nonEmptyStroke = false;
		for (var i = 1; i < s.Length;)
		{
			if (s[i] == RIGHT_DELIMITER)
			{
				rhythm = new(strokes);
				/*do
				{
					i++;
				} while (i < s.Length && s[i] == RIGHT_DELIMITER);*/

				return i + 1;
			}

			var strokeLength = EnumNameAttribute.TryRead<StrokeType>(s[i..], out var strokeType);
			if (strokeLength <= 0)
				return -1;

			strokes.Add(new(strokeType));
			/*if (strokeType != StrokeType.None)
				nonEmptyStroke = true;*/
			i += strokeLength;
		}

		return -1;

		//if (strokes.Count == 0 && nonEmptyStroke)
		//	return -1;

		//rhythm = new(strokes);
		//return s.Length;
	}

	public override string ToString() => ToString(null);

	public string ToString(ISheetFormatter? formatter)
		=> formatter?.ToString(this)
		?? $"|{string.Join(null, strokes)}|";
}

