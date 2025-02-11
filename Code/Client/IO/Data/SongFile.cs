using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Konves.ChordPro;

namespace Skinnix.RhymeTool.Client.IO.Data;

public delegate void SongFileErrorHandler(SongFile.ErrorType type, SongFile.RangeToken token);

public abstract class SongFile : FlexibleMarkupFile<SongFile.TokenType>
{
	protected Configuration TokenConfiguration { get; }

	public SongFile(Configuration configuration)
		: base(TokenType.Text, configuration.CreateAssignments())
	{
		this.TokenConfiguration = configuration;
	}

	protected virtual bool CombineLines(IList<RangeToken>? previous, IEnumerable<RangeToken> nextLine)
	{
		previous ??= new List<RangeToken>();
		var endsWithEscape = false;
		foreach (RangeToken token in nextLine)
		{
			endsWithEscape = token.Type == TokenType.SingleEscape;
			previous.Add(token);
		}

		return !endsWithEscape;
	}

	protected virtual SongLine ParseLine(IEnumerable<RangeToken> line, SongFileErrorHandler handleError)
	{
		var tokens = line.GetEnumerator();
		if (!tokens.MoveNext())
			return new EmptyLine();

		if (tokens.Current.Type == TokenType.Separator)
		{
			var directiveLine = ParseElementLine(tokens, handleError, true);
			var directive = directiveLine.Elements.OfType<Directive>().FirstOrDefault();
			if (directive is null)
				return new EmptyLine();

			return new DirectiveLine(directive);
		}

		var elementLine = ParseElementLine(tokens, handleError, false);
		if (elementLine.Elements.Count == 0)
			return new EmptyLine();

		return elementLine;
	}

	protected virtual ElementLine ParseElementLine(IEnumerator<RangeToken> tokens, SongFileErrorHandler handleError, bool isDirectiveLine)
	{
		var result = new ElementLine();
		var stack = new Stack<IWithChildren>();
		if (isDirectiveLine)
		{
			var directive = new Directive();
			stack.Push(directive);
			
			var segment = new DirectiveSegment();
			directive.Segments.Add(segment);
			stack.Push(segment);
		}
		else
		{
			stack.Push(result);
		}

		var skipMoveNext = false;
		for (var current = tokens.Current; skipMoveNext || tokens.MoveNext(); current = tokens.Current)
		{
			skipMoveNext = false;
			switch (current.Type)
			{
				case TokenType.Separator:
					stack.PopUntil<IWithChildren, IAddChildren<DirectiveSegment>>(out var addSegment);
					if (addSegment is null)
					{
						handleError(ErrorType.UnexpectedToken, current);
						break;
					}

					var newSegment = new DirectiveSegment();
					addSegment.Add(newSegment);
					stack.Push(newSegment);
					break;

				case TokenType.DirectiveStart:
					if (!tokens.MoveNext())
					{
						skipMoveNext = true;
						handleError(ErrorType.UnexpectedEnd, current);
						break;
					}
					skipMoveNext = true;

					stack.PopUntil<IWithChildren, IAddChildren<Directive>>(out var addDirective);
					if (addDirective is null)
					{
						handleError(ErrorType.UnexpectedToken, current);
						break;
					}

					var directive = new Directive();
					addDirective.Add(directive); 
					if (current.Type == TokenType.DirectiveEnd)
						break;

					stack.Push(directive);
					var segment = new DirectiveSegment();
					directive.Segments.Add(segment);
					stack.Push(segment);
					break;

				case TokenType.DirectiveEnd:
					stack.PopUntil<IWithChildren, Directive>(out var lastDirective);
					if (lastDirective is not null)
						stack.Pop();
					else
						handleError(ErrorType.UnexpectedToken, current);
					break;

				case TokenType.AttachmentStart:
					if (!tokens.MoveNext())
					{
						skipMoveNext = true;
						handleError(ErrorType.UnexpectedEnd, current);
						break;
					}
					skipMoveNext = true;

					stack.PopUntil<IWithChildren, IAddChildren<Attachment>>(out var addAttachment);
					if (addAttachment is null)
					{
						handleError(ErrorType.UnexpectedToken, current);
						break;
					}

					var attachment = new Attachment();
					addAttachment.Add(attachment);
					if (current.Type == TokenType.AttachmentEnd)
						break;

					stack.Push(attachment);
					break;

				case TokenType.AttachmentEnd:
					stack.PopUntil<IWithChildren, Attachment>(out var lastAttachment);
					if (lastAttachment is not null)
						stack.Pop();
					else
						handleError(ErrorType.UnexpectedToken, current);
					break;

				case TokenType.Text:
					stack.PopUntil<IWithChildren, IAddChildren<Text>>(out var addText);
					if (addText is null)
					{
						handleError(ErrorType.UnexpectedToken, current);
						break;
					}

					var text = new Text(current.Value);
					addText.Add(text);
					break;

				default:
					handleError(ErrorType.UnexpectedToken, current);
					break;
			}
		}

		return result;
	}

	public readonly record struct Configuration(
		char DirectiveStartChar = '{', char DirectiveEndChar = '}',
		char SeparatorChar = '$', char EscapeChar = '\\',
		char AttachmentStartChar = '[', char AttachmentEndChar = ']')
	{
		public static Configuration Default { get; } = new();

		public bool IsEscapable(char c)
			=> c == SeparatorChar || c == DirectiveStartChar
			|| c == DirectiveEndChar || c == EscapeChar
			|| c == AttachmentStartChar || c == AttachmentEndChar;

		public CharAssignment[] CreateAssignments() =>
		[
			new(DirectiveStartChar, TokenType.DirectiveStart),
			new(DirectiveEndChar, TokenType.DirectiveEnd),
			new(SeparatorChar, TokenType.Separator),
			new(EscapeChar, TokenType.SingleEscape),
			new(AttachmentStartChar, TokenType.AttachmentStart),
			new(AttachmentEndChar, TokenType.AttachmentEnd),
		];
	}

	public enum TokenType
	{
		Unknown,
		Text,
		DirectiveStart,
		DirectiveEnd,
		Separator,
		SingleEscape,
		AttachmentStart,
		AttachmentEnd,
	}

	public enum ErrorType
	{
		UnexpectedToken,
		UnexpectedEnd,
	}

	//public struct Consumer<T>
	//{
	//	private readonly IEnumerator<T> enumerator;

	//	private bool hasNext;

	//	public bool HasEnded { get; private set; }
	//	[MaybeNull] public T Current { get; private set; }

	//	public Consumer(IEnumerable<T> values)
	//	{
	//		enumerator = values.GetEnumerator();
	//		HasEnded = !enumerator.MoveNext();
	//		if (!HasEnded)
	//		{
	//			Current = enumerator.Current;
	//			hasNext = enumerator.MoveNext();
	//		}
	//	}

	//	public bool MoveNext()
	//	{
	//		hasNext = enumerator.MoveNext();
	//		return hasNext;
	//	}

	//	public bool TryConsume(out T value)
	//	{
	//		if (enumerator.MoveNext())
	//		{
	//			value = enumerator.Current;
	//			return true;
	//		}
	//		value = default;
	//		return false;
	//	}
	//}

	public abstract class SongFileElement
	{
		public abstract void Write(StringBuilder builder, Configuration configuration);
	}

	public interface IWithChildren
	{
		int Count { get; }
	}

	public interface IGetChildren<out T> : IWithChildren
	{
		IEnumerable<T> GetChildren();
	}

	public interface IAddChildren<in T> : IWithChildren
	{
		void Add(T child);
	}

	public interface IWithChildren<T> : IGetChildren<T>, IAddChildren<T>
	{
		int IWithChildren.Count => Elements.Count;
		IList<T> Elements { get; }

		IEnumerable<T> IGetChildren<T>.GetChildren() => Elements;
		void IAddChildren<T>.Add(T child) => Elements.Add(child);
	}

	public abstract class SongLine : SongFileElement;

	public sealed class EmptyLine : SongLine
	{
		public override void Write(StringBuilder builder, Configuration configuration) { }
	}

	public sealed class DirectiveLine : SongLine
	{
		public Directive Directive { get; }

		public DirectiveLine(Directive directive)
		{
			Directive = directive;
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			builder.Append(configuration.SeparatorChar);
			Directive.Write(builder, configuration, false, false);
		}
	}

	public sealed class ElementLine : SongLine, IWithChildren<LineElement>
	{
		public IList<LineElement> Elements { get; } = [];

		public ElementLine() { }

		public ElementLine(IEnumerable<LineElement> elements)
		{
			Elements = elements.ToList();
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			foreach (var element in Elements)
			{
				element.Write(builder, configuration);
			}
		}
	}

	public abstract class LineElement : SongFileElement;

	public sealed class Text : LineElement
	{
		public LineString Value { get; set; }

		public Text(LineString text)
		{
			Value = text;
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			foreach (var character in Value)
			{
				if (configuration.IsEscapable(character.Char))
					builder.Append(configuration.EscapeChar);

				builder.Append(character.Char);
			}
		}
	}

	public sealed class Attachment : LineElement, IWithChildren<LineElement>
	{
		public IList<LineElement> Elements { get; } = [];

		public Attachment() { }

		public Attachment(IEnumerable<LineElement> elements)
		{
			Elements = elements.ToList();
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			foreach (var element in Elements)
			{
				element.Write(builder, configuration);
			}
		}
	}

	public sealed class Directive : LineElement, IWithChildren<DirectiveSegment>
	{
		public IList<DirectiveSegment> Segments { get; } = [];
		IList<DirectiveSegment> IWithChildren<DirectiveSegment>.Elements => Segments;

		public Directive() { }

		public Directive(IEnumerable<DirectiveSegment> segments)
		{
			Segments = segments.ToList();
		}

		public override void Write(StringBuilder builder, Configuration configuration)
			=> Write(builder, configuration, true, true);

		public void Write(StringBuilder builder, Configuration configuration, bool start, bool end)
		{
			if (start)
				builder.Append(configuration.DirectiveStartChar);

			var first = true;
			foreach (var segment in Segments)
			{
				if (!first)
					builder.Append(configuration.SeparatorChar);

				segment.Write(builder, configuration);

				first = false;
			}

			if (end)
				builder.Append(configuration.DirectiveEndChar);
		}
	}

	public sealed class DirectiveSegment : SongFileElement, IWithChildren<LineElement>
	{
		public IList<LineElement> Elements { get; } = [];

		public DirectiveSegment() { }

		public DirectiveSegment(IEnumerable<LineElement> elements)
		{
			Elements = elements.ToList();
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			foreach (var element in Elements)
			{
				element.Write(builder, configuration);
			}
		}
	}
}

public static class CollectionExtensions
{
	public static void PopUntil<T, TTarget>(this Stack<T> stack, out TTarget? target)
		where TTarget : class, T
	{
		target = null;
		while (stack.TryPeek(out var top) && (target = top as TTarget) is null)
			stack.Pop();
	}
}