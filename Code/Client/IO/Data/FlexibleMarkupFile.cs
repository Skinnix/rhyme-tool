using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.IO.Data;

public abstract class FlexibleMarkupFile
{
	protected readonly char StartChar = '{';
	protected readonly char EndChar = '}';
	protected readonly char SeparatorChar = '$';
	protected readonly char EscapeChar = '\\';

	protected FlexibleMarkupFile(char startChar = '{', char endChar = '}',
		char separatorChar = '$', char escapeChar = '\\')
	{
		StartChar = startChar;
		EndChar = endChar;
		SeparatorChar = separatorChar;
		EscapeChar = escapeChar;
	}

	protected bool IsEscapable(char c)
		=> c == SeparatorChar || c == StartChar || c == EndChar || c == EscapeChar;

	//public static FlexibleMarkupFile? TryCreate(string line)
	//{
	//	if (line.Length < 4)
	//		return null;

	//	var separatorChar = line[0];
	//	var startChar = line[1];
	//	var escapeChar = line[2];
	//	var endChar = line[^1];

	//	if (new[] { separatorChar, startChar, escapeChar, endChar }.Distinct().Count() != 4)
	//		return null;

	//	return new FlexibleMarkupFile(separatorChar, startChar, endChar);
	//}

	public enum SimpleTokenType
	{
		Text,
		Separator,
		Start,
		End,
		SingleEscape,
	}

	public enum SingleEscapeHandling
	{
		Ignore,
		Include,
		Text,
	}
}

public abstract class FlexibleMarkupFile<TTokenType> : FlexibleMarkupFile
	where TTokenType : struct, Enum
{
	private readonly TTokenType defaultType;
	private readonly CharAssignment[] assignments;

	protected FlexibleMarkupFile(TTokenType defaultType, params IEnumerable<CharAssignment> assignments)
	{
		this.defaultType = defaultType;
		this.assignments = assignments.ToArray();
	}

	protected virtual IEnumerable<CharToken> LexChars(ReadOnlySpan<char> line,
		CharAssignment? escape, TTokenType escapedType, TTokenType escapeFailedType)
	{
		int i, escapedIndex;
		for (i = 0, escapedIndex = 0; i < line.Length; i++, escapedIndex++)
		{
			var character = new LineChar(line[i], i, escapedIndex);
			if (escape is not null && character.Char == escape.Value.Char)
			{
				if (i + 1 < line.Length && IsEscapable(line[i + 1]))
				{
					i++;
					var next = new LineChar(line[i], i, escapedIndex);
					yield return new(escapedType, character);
				}
				else
				{
					yield return new(escapeFailedType, character);
				}
			}
			else
			{
				foreach (var assignment in assignments)
				{
					if (character.Char == assignment.Char)
					{
						yield return new(assignment.Type, character);
						break;
					}
				}

				yield return new(defaultType, character);
			}
		}
	}

	protected virtual IEnumerable<RangeToken> CombineText(IEnumerable<CharToken> chars, TTokenType textType)
	{
		List<LineChar>? buffer = null;
		foreach (var token in chars)
		{
			if (textType.Equals(token.Type))
			{
				(buffer ??= new()).Add(token.Char);
				continue;
			}

			if (buffer is not null)
			{
				yield return new(textType, new(buffer));
				buffer = null;
			}

			yield return new(token.Type, new(token.Char));
		}

		if (buffer is not null)
			yield return new(textType, new(buffer));
	}

	public readonly record struct CharAssignment(char Char, TTokenType Type);

	public readonly record struct LineChar(char Char, int Index, int EscapedIndex)
	{
		public static LineChar[] FromString(string s)
		{
			var chars = new LineChar[s.Length];
			for (var i = 0; i < s.Length; i++)
				chars[i] = new LineChar(s[i], i, i);

			return chars;
		}
	}

	public readonly struct LineString : IReadOnlyList<LineChar>
	{
		private readonly LineChar[] chars;

		public LineString(LineChar[] chars)
		{
			this.chars = chars;
		}

		public LineString(IEnumerable<LineChar> chars)
		{
			if (chars is LineChar[] array)
			{
				this.chars = array;
			}
			else if (chars is IReadOnlyCollection<LineChar> collection)
			{
				this.chars = new LineChar[collection.Count];
				foreach (var (i, c) in collection.Index())
					this.chars[i] = c;
			}
			else
			{
				this.chars = chars.ToArray();
			}
		}

		public LineString(LineChar character)
		{
			chars = [character];
		}

		public int Count => chars.Length;
		public LineChar this[int index] => chars[index];

		public IEnumerator<LineChar> GetEnumerator() => ArrayEnumerator.Create(chars);
		IEnumerator IEnumerable.GetEnumerator() => chars.GetEnumerator();

		public static LineString operator +(LineString a, LineString b)
		{
			var chars = new LineChar[a.Count + b.Count];
			a.chars.CopyTo(chars, 0);
			b.chars.CopyTo(chars, a.Count);
			return new(chars);
		}
	}

	public readonly record struct CharToken(TTokenType Type, LineChar Char);

	public readonly record struct RangeToken(TTokenType Type, LineString Value);





	public interface IWriterSettings
	{
		char SeparatorChar { get; }
		char StartChar { get; }
		char EndChar { get; }
	}

	public interface IFileComponent
	{
		void Write(IWriterSettings settings, StringBuilder builder);
	}

	public interface IParseError
	{
		string Message { get; }
	}

	public interface ILine : IFileComponent;

	public interface IDirectiveLine : ILine
	{
		ILineDirective Directive { get; }
	}

	public interface IElementLine : ILine, IHasElements;

	public interface IHasElements
	{
		IReadOnlyList<IElement> Elements { get; }
	}

	public interface IElement : IFileComponent;

	public interface ITextElement : IElement
	{
		ReadOnlySpan<char> Text { get; }
	}

	public interface IDirective : IFileComponent
	{
		string Key { get; }
	}

	public interface IContentDirective : IDirective, IHasElements;

	public interface ILineDirective : IDirective;

	public interface IInlineDirective : IDirective, IElement;

	#region Default Implementation
	#endregion
}
