using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Konves.ChordPro;

using LineChar = System.Char;
using LineString = System.String;

namespace Skinnix.RhymeTool.Client.IO;

public delegate void SongFileErrorHandler(SongFile.ErrorType type, SongFile.RangeToken token, SongFile.TokenType? expected);

public abstract class SongFile : FlexibleMarkupFile<SongFile.TokenType>
{
	public Configuration TokenConfiguration { get; }

	public SongFile(Configuration configuration)
	{
		TokenConfiguration = configuration;
	}

	protected virtual IEnumerable<CharToken> LexChars(string line)
		=> LexChars(line, TokenConfiguration.Escape, TokenType.Text,
			TokenConfiguration.IsEscapable, TokenType.Text,
			TokenConfiguration.CreateAssignments());

	protected virtual IEnumerable<RangeToken> CombineText(IEnumerable<CharToken> tokens)
		=> CombineText(tokens, TokenType.Text);

	protected virtual bool CombineLines(IList<RangeToken>? previous, IEnumerable<RangeToken> nextLine)
	{
		previous ??= new List<RangeToken>();
		var endsWithEscape = false;
		foreach (var token in nextLine)
		{
			endsWithEscape = token.Type == TokenType.SingleEscape;
			previous.Add(token);
		}

		if (!endsWithEscape)
			return true;

		previous[previous.Count - 1] = previous[previous.Count - 1] with
		{
			Type = TokenType.LineBreak,
		};
		return false;
	}

	protected virtual SongLine ParseLine(IEnumerable<RangeToken> line, SongFileErrorHandler handleError)
	{
		var tokens = line.GetEnumerator();
		if (!tokens.MoveNext())
			return new EmptyLine();

		if (tokens.Current.Type == TokenType.Separator)
		{
			if (!tokens.MoveNext())
			{
				handleError(ErrorType.UnexpectedEnd, default, (new DirectiveSegment() as IWithChildren).PossibleChildren);
				return new DirectiveLine(new Directive());
			}

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
		var stack = new HierarchyStack<IWithChildren>();
		if (isDirectiveLine)
		{
			var directive = new Directive();
			result.Elements.Add(directive);
			stack.Push(directive);

			var segment = new DirectiveSegment();
			directive.Segments.Add(segment);
			stack.Push(segment);
		}
		else
			stack.Push(result);

		var skipMoveNext = true;
		RangeToken current;
		while (skipMoveNext || tokens.MoveNext())
		{
			skipMoveNext = false;
			current = tokens.Current;

			switch (current.Type)
			{
				case TokenType.Separator:
					var addSegment = stack.Find<IAddChildren<DirectiveSegment>>();
					if (!HandleFindErrors(addSegment, current, handleError))
						break;

					addSegment.PopUntilTop();
					var newSegment = new DirectiveSegment();
					addSegment.Target!.Add(newSegment);
					stack.Push(newSegment);
					break;

				case TokenType.DirectiveStart:
					if (!tokens.MoveNext())
					{
						skipMoveNext = true;
						handleError(ErrorType.UnexpectedEnd, current, stack.PossibleChildren());
						break;
					}
					current = tokens.Current;
					skipMoveNext = true;

					var addDirective = stack.Find<IAddChildren<Directive>>();
					if (!HandleFindErrors(addDirective, current, handleError))
						break;

					addDirective.PopUntilTop();
					var directive = new Directive();
					addDirective.Target!.Add(directive);
					if (current.Type == TokenType.DirectiveEnd)
						break;

					stack.Push(directive);
					var segment = new DirectiveSegment();
					directive.Segments.Add(segment);
					stack.Push(segment);
					break;

				case TokenType.DirectiveEnd:
					var lastDirective = stack.Find<Directive>();
					if (!HandleFindErrors(lastDirective, current, handleError))
						break;

					lastDirective.PopUntilTop();
					stack.Pop();
					break;

				case TokenType.AttachmentStart:
					if (!tokens.MoveNext())
					{
						skipMoveNext = true;
						handleError(ErrorType.UnexpectedEnd, current, stack.PossibleChildren());
						break;
					}
					current = tokens.Current;
					skipMoveNext = true;

					var addAttachment = stack.Find<IAddChildren<Attachment>>();
					if (!HandleFindErrors(addAttachment, current, handleError))
						break;

					addAttachment.PopUntilTop();
					var attachment = new Attachment();
					addAttachment.Target!.Add(attachment);
					if (current.Type == TokenType.AttachmentEnd)
						break;

					stack.Push(attachment);
					break;

				case TokenType.AttachmentEnd:
					var lastAttachment = stack.Find<Attachment>();
					if (!HandleFindErrors(lastAttachment, current, handleError))
						break;

					lastAttachment.PopUntilTop();
					stack.Pop();
					break;

				case TokenType.Text:
					var addText = stack.Find<IAddChildren<Text>>();
					if (!HandleFindErrors(addText, current, handleError))
						break;

					//addText.PopUntilTop();
					var text = new Text(current.Value);
					addText.Target!.Add(text);
					break;

				case TokenType.LineBreak:
					var addLineBreak = stack.Find<IAddChildren<Instruction.LineBreak>>();
					if (!HandleFindErrors(addLineBreak, current, handleError))
						break;

					//addLineBreak.PopUntilTop();
					var lineBreak = new Instruction.LineBreak();
					addLineBreak.Target!.Add(lineBreak);
					break;

				default:
					handleError(ErrorType.UnexpectedToken, current, stack.PossibleChildren());
					break;
			}
		}

		while (stack.TryPop(out var top))
		{
			var closingType = top.ClosedBy;
			if (closingType is null or TokenType.None)
				continue;

			if (!isDirectiveLine || stack.Count == 0)
				handleError(ErrorType.UnexpectedEnd, default, closingType);
		}

		return result;

		static bool HandleFindErrors<TChild>(HierarchyStack<IWithChildren>.FindResult<TChild> findResult,
			RangeToken current, SongFileErrorHandler handleError)
			where TChild : class, IWithChildren
		{
			if (!findResult.Success)
			{
				handleError(ErrorType.NoMatchingParent, current, findResult.Owner.PossibleChildren());
				return false;
			}

			if (findResult.NeedsClose(out var possibleChildren, out var cannotClose))
			{
				if (cannotClose is not null)
					handleError(ErrorType.CannotClose, current, possibleChildren);
				else
					handleError(ErrorType.InvalidChild, current, possibleChildren);

				return false;
			}

			return true;
		}
	}

	public readonly record struct Configuration(
		char DirectiveStartChar = '{', char DirectiveEndChar = '}',
		char SeparatorChar = '$', char EscapeChar = '\\',
		char AttachmentStartChar = '[', char AttachmentEndChar = ']')
	{
		public static Configuration Default { get; } = new(
			DirectiveStartChar: '{', DirectiveEndChar: '}',
			SeparatorChar: '$', EscapeChar: '\\',
			AttachmentStartChar: '[', AttachmentEndChar: ']');

		public CharAssignment DirectiveStart => new(DirectiveStartChar, TokenType.DirectiveStart);
		public CharAssignment DirectiveEnd => new(DirectiveEndChar, TokenType.DirectiveEnd);
		public CharAssignment Separator => new(SeparatorChar, TokenType.Separator);
		public CharAssignment Escape => new(EscapeChar, TokenType.SingleEscape);
		public CharAssignment AttachmentStart => new(AttachmentStartChar, TokenType.AttachmentStart);
		public CharAssignment AttachmentEnd => new(AttachmentEndChar, TokenType.AttachmentEnd);

		public bool IsValid { get; } = new[] {
			DirectiveStartChar, DirectiveEndChar,
			SeparatorChar, EscapeChar,
			AttachmentStartChar, AttachmentEndChar,
		}.Distinct().Count() == 6;

		public bool IsEscapable(char c)
			=> c == SeparatorChar || c == DirectiveStartChar
			|| c == DirectiveEndChar || c == EscapeChar
			|| c == AttachmentStartChar || c == AttachmentEndChar;

		public CharAssignment[] CreateAssignments() =>
		[
			DirectiveStart, DirectiveEnd,
			Separator, Escape,
			AttachmentStart, AttachmentEnd,
		];
	}

	[Flags]
	public enum TokenType
	{
		None = 0,
		Text = 1,
		DirectiveStart = 2,
		DirectiveEnd = 4,
		Separator = 8,
		SingleEscape = 16,
		AttachmentStart = 32,
		AttachmentEnd = 64,

		LineBreak = 4096,
	}

	public enum ErrorType
	{
		UnexpectedToken,
		NoMatchingParent,
		InvalidChild,
		CannotClose,
		UnexpectedEnd,
	}

	public abstract class SongFileElement
	{
		public abstract void Write(StringBuilder builder, Configuration configuration);
	}

	public interface IWithChildren
	{
		int Count { get; }
		TokenType PossibleChildren { get; }
		TokenType? ClosedBy { get; }
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

		TokenType IWithChildren.PossibleChildren => TokenType.Text | TokenType.DirectiveStart | TokenType.Separator | TokenType.AttachmentStart;
		TokenType? IWithChildren.ClosedBy => TokenType.None;

		public ElementLine() { }

		public ElementLine(IEnumerable<LineElement> elements)
		{
			Elements = elements.ToList();
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			foreach (var element in Elements)
				element.Write(builder, configuration);
		}
	}

	public abstract class LineElement : SongFileElement;

	[DebuggerDisplay("\"{Value}\"")]
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
				if (configuration.IsEscapable(character))
					builder.Append(configuration.EscapeChar);

				builder.Append(character);
			}
		}

		public override string ToString() => Value;
	}

	[DebuggerDisplay("{ToDebuggerString()}")]
	public sealed class Attachment : LineElement, IWithChildren<LineElement>
	{
		public IList<LineElement> Elements { get; } = [];

		TokenType IWithChildren.PossibleChildren => TokenType.AttachmentEnd | TokenType.Text;
		TokenType? IWithChildren.ClosedBy => TokenType.AttachmentEnd;

		public Attachment() { }

		public Attachment(params IEnumerable<LineElement> elements)
		{
			Elements = elements.ToList();
		}

		public static Attachment FromText(LineString text)
			=> new(new Text(text));

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			builder.Append(configuration.AttachmentStartChar);

			foreach (var element in Elements)
				element.Write(builder, configuration);

			builder.Append(configuration.AttachmentEndChar);
		}

		private string ToDebuggerString() => $"[{string.Join(null, Elements)}]";

		public override string ToString() => $"[{string.Join(null, Elements)}]";
	}

	public sealed class Directive : LineElement, IWithChildren<DirectiveSegment>
	{
		public IList<DirectiveSegment> Segments { get; } = [];
		IList<DirectiveSegment> IWithChildren<DirectiveSegment>.Elements => Segments;

		TokenType IWithChildren.PossibleChildren => TokenType.AttachmentStart | TokenType.Text | TokenType.Separator | TokenType.DirectiveEnd;
		TokenType? IWithChildren.ClosedBy => TokenType.DirectiveEnd;

		public Directive() { }

		public Directive(params IEnumerable<DirectiveSegment> segments)
		{
			Segments = segments.ToList();
		}

		public static Directive WithKey(LineString key, params IEnumerable<DirectiveSegment> segments)
			=> new(segments.Prepend(new(new Text(key))));

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

		public override string ToString() => $"{{{string.Join('$', Segments)}}}";
	}

	public abstract class Instruction : LineElement
	{
		public sealed class LineBreak : Instruction
		{
			public override void Write(StringBuilder builder, Configuration configuration)
			{
				builder.Append(configuration.EscapeChar);
				builder.AppendLine();
			}

			public override string ToString() => "\n";
		}
	}

	public sealed class DirectiveSegment : SongFileElement, IWithChildren<LineElement>
	{
		public IList<LineElement> Elements { get; } = [];

		TokenType IWithChildren.PossibleChildren => TokenType.Separator | TokenType.DirectiveEnd | TokenType.Text | TokenType.AttachmentStart;
		TokenType? IWithChildren.ClosedBy => null;

		public DirectiveSegment() { }

		public DirectiveSegment(params IEnumerable<LineElement> elements)
		{
			Elements = elements.ToList();
		}

		public override void Write(StringBuilder builder, Configuration configuration)
		{
			foreach (var element in Elements)
				element.Write(builder, configuration);
		}

		public override string ToString() => string.Join(null, Elements);
	}

	internal class HierarchyStack<T> : Stack<T>
		where T : class
	{
		public FindResult<TTarget> Find<TTarget>()
			where TTarget : class, T
		{
			var onTop = new Queue<T>();
			foreach (var item in this)
			{
				if (item is TTarget t)
					return new(this, t, onTop);

				onTop.Enqueue(item);
			}

			return new(this, null, onTop);
		}

		public IEnumerable<T> PopUntil<TTarget>(out TTarget? target)
			where TTarget : class, T
		{
			var result = new List<T>();
			target = null;
			while (TryPeek(out var top) && (target = top as TTarget) is null)
				result.Add(Pop());
			return result;
		}

		public readonly record struct FindResult<TTarget>(HierarchyStack<T> Owner, TTarget? Target, IReadOnlyCollection<T> OnTop)
			where TTarget : class, T
		{
			[MemberNotNullWhen(true, nameof(Target))]
			public bool Success => Target is not null;

			public void PopUntilTop()
			{
				if (!Success)
					return;

				while (Owner.TryPeek(out var top) && top != Target)
					Owner.Pop();
			}
		}

		public readonly record struct PopUntilAction<TTarget>(HierarchyStack<T> Owner, TTarget? Target, IReadOnlyCollection<T> Popped)
			where TTarget : class, T
		{
			public void Apply()
			{
				foreach (var item in Popped.Reverse())
					Owner.Push(item);
			}
		}
	}
}

public static class CollectionExtensions
{
	public static SongFile.TokenType PossibleChildren(this Stack<SongFile.IWithChildren> stack)
		=> !stack.TryPeek(out var top) ? SongFile.TokenType.None : top.PossibleChildren;

	internal static bool NeedsClose<T, TTarget>(this SongFile.HierarchyStack<T>.FindResult<TTarget> findResult, out SongFile.TokenType possibleChildren,
		out T? cannotClose)
		where T : class, SongFile.IWithChildren
		where TTarget : class, T
	{
		possibleChildren = SongFile.TokenType.None;
		foreach (var item in findResult.OnTop)
		{
			var closingType = item.ClosedBy;
			if (closingType is not null)
			{
				possibleChildren |= closingType.Value | item.PossibleChildren;

				if (closingType.Value == SongFile.TokenType.None)
				{
					cannotClose = item;
					return true;
				}

				cannotClose = null;
				return true;
			}

			possibleChildren |= item.PossibleChildren;
		}

		cannotClose = null;
		return false;
	}

	public static void PopUntil<T, TTarget>(this Stack<T> stack, out TTarget? target)
		where TTarget : class, T
	{
		target = null;
		while (stack.TryPeek(out var top) && (target = top as TTarget) is null)
			stack.Pop();
	}
}