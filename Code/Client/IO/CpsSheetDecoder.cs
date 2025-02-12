using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.IO;

public class CpsSheetDecoder : SheetDecoderBase<CpsFile.SongLine>
{
	private readonly List<SheetLine> lines = new();

	public CpsSheetDecoder(ISheetEditorFormatter formatter)
		: base(formatter)
	{ }

	public CpsSheetDecoder()
		: base(DefaultSheetFormatter.Instance)
	{ }

	public override void ProcessLine(CpsFile.SongLine line)
	{
		switch (line)
		{
			case CpsFile.EmptyLine:
				lines.Add(new SheetEmptyLine());
				break;

			case CpsFile.DirectiveLine directiveLine:
				lines.Add(TryRead(directiveLine) ?? ReadFallback(directiveLine));
				break;

			case CpsFile.ElementLine elementLine:
				lines.Add(Read(elementLine));
				break;

			default:
				var fallbackComponents = new List<SheetVarietyLine.VarietyComponent>();
				int read;
				var text = line.ToString();
				for (var offset = 0;
					(read = SheetVarietyLine.ComponentContent.TryRead(text.AsSpan(offset..), out var content, Formatter)) != 0 && content is not null;
					offset += read)
				{
					var component = new SheetVarietyLine.VarietyComponent(content.Value);
					fallbackComponents.Add(component);
				}
				lines.Add(new SheetVarietyLine(fallbackComponents));
				break;
		}
	}

	public override SheetDocument Finalize() => new(lines);

	protected virtual SheetLine? TryRead(CpsFile.DirectiveLine directiveLine)
	{
		if (directiveLine.Directive.Segments.Count == 0)
			return null;

		var firstSegment = directiveLine.Directive.Segments[0];
		if (firstSegment.Elements.WithoutInstructions().FirstOrDefault(out var hasNext) is not CpsFile.Text { Value: string directiveKey } || hasNext)
			return null;

		if (directiveKey == CpsFile.PART_DIRECTIVE)
		{
			if (directiveLine.Directive.Segments.Count != 2)
				return null;

			var valueSegment = directiveLine.Directive.Segments[1];
			if (valueSegment.Elements.WithoutInstructions().FirstOrDefault(out hasNext) is not CpsFile.Text { Value: string title } || hasNext)
				return null;

			return new SheetVarietyLine([new SheetVarietyLine.VarietyComponent(SheetVarietyLine.TITLE_START_DELIMITER + title)]);
		}

		if (directiveKey == CpsFile.TAB_DIRECTIVE)
		{
			if (directiveLine.Directive.Segments.Count <= 1)
				return null;

			var tabSegments = directiveLine.Directive.Segments.Skip(1).Select(s => s.Elements.WithoutInstructions()).ToArray();
			if (tabSegments.Any(s => s.Any(e => e is not CpsFile.Text)))
				return null;

			var tabLines = tabSegments.Select(s => string.Join(null, s.Cast<CpsFile.Text>().Select(e => e.Value)));
			var parsed = tabLines.Select(l => SheetDecoderHelper.TryParseTabLine(l, Formatter)).ToArray();
			if (parsed.Any(p => p is null))
				return null;

			var tabLine = SheetDecoderHelper.CreateTabLine(parsed.Select(p => p!.Value).ToArray());
			return tabLine;
		}

		return null;
	}

	protected virtual SheetLine ReadFallback(CpsFile.DirectiveLine directiveLine)
		=> new SheetVarietyLine(ReadElements(directiveLine.Directive.Segments.SelectMany(s => s.Elements.WithoutInstructions())));

	protected virtual SheetVarietyLine Read(CpsFile.ElementLine elementLine)
		=> new(ReadElements(elementLine.Elements.WithoutInstructions()));

	protected virtual IEnumerable<SheetVarietyLine.VarietyComponent> ReadElements(IEnumerable<CpsFile.LineElement> elements)
	{
		(var contentElements, var endAttachment) = ExtractAttachments(elements);
		foreach (var contentElement in contentElements)
		{
			switch (contentElement.Element)
			{
				case CpsFile.Text text:
					int read;
					var wasSpace = true;
					for (var offset = 0;
						(read = SheetVarietyLine.ComponentContent.TryRead(text.Value.AsSpan(offset..), out var content, Formatter)) != 0 && content is not null;
						offset += read)
					{
						if (!wasSpace || (offset + read < text.Value.Length && !char.IsWhiteSpace(text.Value[offset + read]))
							&& content.Value.Type is SheetVarietyLine.ContentType.Chord or SheetVarietyLine.ContentType.Fingering)
						{
							read = SheetVarietyLine.ComponentContent.TryRead(text.Value.AsSpan(offset..), out content, Formatter, SheetVarietyLine.SpecialContentType.Text);

							if (read == 0 || content is null)
								break;
						}

						var component = new SheetVarietyLine.VarietyComponent(content.Value);
						var contentAttachments = contentElement.Attachments.Where(a => a.Offset >= offset && a.Offset < offset + read);
						foreach (var attachment in contentAttachments)
						{
							var attachmentElements = attachment.Attachment.Elements
								.WithoutInstructions()
								.Select(e => e is CpsFile.Text text ? text.Value : e is CpsFile.Instruction ? null : e.ToString());
							var attachmentText = string.Join(null, attachmentElements);
							var attachmentContent = SheetVarietyLine.ComponentContent.FromString(attachmentText, Formatter);
							component.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(new(attachment.Offset - offset), attachmentContent));
						}

						yield return component;
					}
					break;

				default:
					var defaultContent = SheetVarietyLine.ComponentContent.FromString(contentElement.Element.ToString() ?? string.Empty, Formatter);
					var defaultComponent = new SheetVarietyLine.VarietyComponent(defaultContent);

					foreach (var attachment in contentElement.Attachments)
					{
						var attachmentElements = attachment.Attachment.Elements
							.WithoutInstructions()
							.Select(e => e is CpsFile.Text text ? text.Value : e is CpsFile.Instruction ? null : e.ToString());
						var attachmentText = string.Join(null, attachmentElements);
						var attachmentContent = SheetVarietyLine.ComponentContent.FromString(attachmentText, Formatter);
						defaultComponent.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(new(attachment.Offset), attachmentContent));
					}

					yield return defaultComponent;
					break;
			}
		}

		if (endAttachment is not null)
		{
			var endComponent = new SheetVarietyLine.VarietyComponent(" ");
			var attachmentElements = endAttachment.Elements
				.WithoutInstructions()
				.Select(e => e is CpsFile.Text text ? text.Value : e is CpsFile.Instruction ? null : e.ToString());
			var attachmentText = string.Join(null, attachmentElements);
			var attachmentContent = SheetVarietyLine.ComponentContent.FromString(attachmentText, Formatter);
			endComponent.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(ContentOffset.Zero, attachmentContent));

			yield return endComponent;
		}
	}

	private static (List<AttachmentElement> Elements, CpsFile.Attachment? EndAttachment) ExtractAttachments(IEnumerable<CpsFile.LineElement> elements)
	{
		var content = new List<AttachmentElement>();
		var waitingAttachments = new List<CpsFile.Attachment>();
		foreach (var element in elements)
		{
			if (element is CpsFile.Attachment attachment)
			{
				waitingAttachments.Add(attachment);
				continue;
			}

			var lastElement = content.LastOrDefault();
			var attachmentOffset = 0;
			AttachmentElement attachmentElement;
			if (lastElement?.Element is CpsFile.Text lastText && element is CpsFile.Text text)
			{
				attachmentOffset = lastText.Value.Length;
				lastElement.Element = new CpsFile.Text(lastText.Value + text.Value);
				attachmentElement = lastElement;
			}
			else
			{
				attachmentElement = new(element);
				content.Add(attachmentElement);
			}

			if (waitingAttachments.Count != 0)
			{
				var combinedAttachment = new CpsFile.Attachment(waitingAttachments.SelectMany(a => a.Elements.WithoutInstructions()).ToArray());
				attachmentElement.Attachments.Add(new(combinedAttachment, attachmentOffset));
			}

			waitingAttachments.Clear();
		}

		if (waitingAttachments.Count != 0)
		{
			var combinedAttachment = new CpsFile.Attachment(waitingAttachments.SelectMany(a => a.Elements.WithoutInstructions()).ToArray());
			return (content, combinedAttachment);
		}

		return (content, null);
	}

	private class AttachmentElement(CpsFile.LineElement element)
	{
		public CpsFile.LineElement Element { get; set; } = element;
		public List<AnchoredAttachment> Attachments { get; } = new();
	}

	private readonly record struct AnchoredAttachment(CpsFile.Attachment Attachment, int Offset);
}

public class CpsSheetDecoderReader : SheetDecoderReader<CpsFile.SongLine, CpsSheetDecoder, (StreamReader Reader, CpsFile File, List<CpsFile.RangeToken> CurrentTokens)>
{
	public static new CpsSheetDecoderReader Default { get; } = new();

	protected override CpsSheetDecoder GetDecoder() => new();

	protected override (StreamReader Reader, CpsFile File, List<CpsFile.RangeToken> CurrentTokens) Open(Stream stream, bool leaveOpen = false)
	{
		var reader = new StreamReader(stream, leaveOpen: leaveOpen);
		var firstLine = reader.ReadLine();
		var file = firstLine is null ? new CpsFile()
			: CpsFile.TryCreate(firstLine)
			?? new CpsFile();

		return (reader, file, new());
	}

	protected override async Task<(StreamReader Reader, CpsFile File, List<CpsFile.RangeToken> CurrentTokens)> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		var reader = new StreamReader(stream, leaveOpen: leaveOpen);
		var firstLine = await reader.ReadLineAsync(cancellation);
		var file = firstLine is null ? new CpsFile()
			: CpsFile.TryCreate(firstLine)
			?? new CpsFile();

		return (reader, file, new());
	}

	internal SheetDocument ReadSheet(StreamReader reader, CpsFile file, List<FlexibleMarkupFile<SongFile.TokenType>.RangeToken> currentTokens, CpsSheetDecoder decoder)
		=> ReadSheet((reader, file, currentTokens), decoder);

	internal Task<SheetDocument> ReadSheetAsync(StreamReader reader, CpsFile file, List<FlexibleMarkupFile<SongFile.TokenType>.RangeToken> currentTokens, CpsSheetDecoder decoder, CancellationToken cancellation = default)
		=> ReadSheetAsync((reader, file, currentTokens), decoder, cancellation);

	internal CpsFile.SongLine? TryReadLine(StreamReader reader, CpsFile file, List<CpsFile.RangeToken> currentTokens)
		=> TryReadLine((reader, file, currentTokens));

	protected override CpsFile.SongLine? TryReadLine((StreamReader Reader, CpsFile File, List<CpsFile.RangeToken> CurrentTokens) state)
	{
		string? line;
		do
		{
			line = state.Reader.ReadLine();
			if (line is null)
			{
				if (state.CurrentTokens.Count == 0)
					return null;

				var parsedLast = state.File.ParseLine(state.CurrentTokens);
				state.CurrentTokens.Clear();
				return parsedLast;
			}

			var parsed = state.File.TryParseLine(state.CurrentTokens, line);
			if (parsed is null)
				continue;

			state.CurrentTokens.Clear();
			return parsed;
		}
		while (true);
	}

	internal Task<CpsFile.SongLine?> TryReadLineAsync(StreamReader reader, CpsFile file, List<CpsFile.RangeToken> currentTokens)
		=> TryReadLineAsync((reader, file, currentTokens));

	protected override async Task<CpsFile.SongLine?> TryReadLineAsync((StreamReader Reader, CpsFile File, List<CpsFile.RangeToken> CurrentTokens) state, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		string? line;
		cancellation.ThrowIfCancellationRequested();
		do
		{
			line = await state.Reader.ReadLineAsync(cancellation);
			if (line is null)
			{
				if (state.CurrentTokens.Count == 0)
					return null;

				var parsedLast = state.File.ParseLine(state.CurrentTokens);
				state.CurrentTokens.Clear();
				return parsedLast;
			}

			var parsed = state.File.TryParseLine(state.CurrentTokens, line);
			if (parsed is null)
				continue;

			state.CurrentTokens.Clear();
			return parsed;
		}
		while (true);
	}
}

public static class CpsSheetEncoderExtensions
{
	public static IEnumerable<T> WithoutInstructions<T>(this IEnumerable<T> elements)
		where T : SongFile.LineElement
		=> elements.Where(e => e is not SongFile.Instruction);

	public static T? FirstOrDefault<T>(this IEnumerable<T> elements, out bool hasNext)
		where T : SongFile.LineElement
	{
		var enumerator = elements.GetEnumerator();
		if (!enumerator.MoveNext())
		{
			hasNext = false;
			return default;
		}

		var result = enumerator.Current;
		hasNext = enumerator.MoveNext();
		return result;
	}
}
