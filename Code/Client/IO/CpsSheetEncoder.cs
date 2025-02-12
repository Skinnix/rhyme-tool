using System.Text;
using Konves.ChordPro;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public class CpsSheetEncoder : SheetEncoderBase<CpsFile.SongLine>
{
	public CpsSheetEncoder(ISheetBuilderFormatter? formatter = null)
		: base(formatter ?? DefaultSheetEncoder.DefaultWriterFormatter.Instance)
	{ }

	public override IEnumerable<CpsFile.SongLine?> ProcessLines(SheetDocument document)
	{
		foreach (var sheetLine in document.Lines.GetLinesWithContext())
		{
			if (sheetLine.Line is SheetEmptyLine)
			{
				yield return new CpsFile.EmptyLine();
				continue;
			}

			if (sheetLine.Line is SheetTabLine tabLine)
			{
				var tabLines = sheetLine.CreateDisplayLines(Formatter).Select(d => d.GetElements().Select(e => new CpsFile.Text(e.ToString())));
				var tabSegments = tabLines.Select(l
					=> new CpsFile.DirectiveSegment(l.Prepend<CpsFile.LineElement>(new CpsFile.Instruction.LineBreak())));
				var tabDirective = CpsFile.Directive.WithKey(CpsFile.TAB_DIRECTIVE, tabSegments);

				yield return new CpsFile.DirectiveLine(tabDirective);
				continue;
			}

			if (sheetLine.Line is SheetVarietyLine varietyLine)
			{
				if (varietyLine.IsTitleLine(out var title))
				{
					var partDirective = CpsFile.Directive.WithKey(CpsFile.PART_DIRECTIVE, new CpsFile.DirectiveSegment(new CpsFile.Text(title)));
					yield return new CpsFile.DirectiveLine(partDirective);
					continue;
				}

				var elements = new List<CpsFile.LineElement>();
				foreach (var component in varietyLine.GetComponents().Index())
				{
					if (component.Item is not SheetVarietyLine.VarietyComponent varietyComponent)
						throw new SheetWriterException("Unbekannter Komponententyp: " + component.GetType().Name);

					var componentText = varietyComponent.Content.ToString(Formatter);
					var syllables = new List<CpsFile.LineElement>();
					SheetVarietyLine.VarietyComponent.Attachment? previousAttachment = null;
					foreach (var attachment in varietyComponent.Attachments.Append(null))
					{
						if (previousAttachment is null)
						{
							if (attachment is null)
								syllables.Add(new CpsFile.Text(componentText));
							else if (attachment.Offset != ContentOffset.Zero)
								syllables.Add(new CpsFile.Text(componentText[..attachment.Offset.Value]));
						}
						else if (previousAttachment is SheetVarietyLine.VarietyComponent.VarietyAttachment varietyAttachment)
						{
							var textBetween = attachment is null
								? componentText[previousAttachment.Offset.Value..]
								: componentText[previousAttachment.Offset.Value..attachment.Offset.Value];

							var attachmentText = varietyAttachment.Content.ToString(Formatter);
							//if (varietyAttachment.Content.Type != SheetVarietyLine.ContentType.Chord)
							//	attachmentText = "*" + attachmentText;
							if (attachmentText.Length != 0)
								syllables.Add(CpsFile.Attachment.FromText(attachmentText));

							if (textBetween.Length != 0)
								syllables.Add(new CpsFile.Text(textBetween));
						}
						else
						{
							throw new SheetWriterException("Unbekannter Attachmenttyp: " + previousAttachment.GetType().Name);
						}

						previousAttachment = attachment;
					}

					elements.AddRange(syllables);
				}

				yield return new CpsFile.ElementLine(elements);
				continue;
			}

			throw new SheetWriterException("Unbekannter Zeilentyp: " + sheetLine.GetType().Name);
		}
	}
}

public class CpsSheetEncoderWriter : SheetEncoderWriter<CpsFile.SongLine, CpsSheetEncoder, (StreamWriter Writer, CpsFile File)>
{
	public const int FORMAT_VERSION = 1;

	public static new CpsSheetEncoderWriter Default { get; } = new();

	protected override CpsSheetEncoder GetEncoder() => new();

	protected override (StreamWriter Writer, CpsFile File) Open(Stream stream, bool leaveOpen = false)
	{
		var writer = new StreamWriter(stream, leaveOpen: leaveOpen);
		var file = new CpsFile();
		writer.WriteLine(file.CreateHeader(FORMAT_VERSION));
		return (writer, file);
	}
	protected override async Task<(StreamWriter Writer, CpsFile File)> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
	{
		var writer = new StreamWriter(stream, leaveOpen: leaveOpen);
		var file = new CpsFile();
		await writer.WriteLineAsync(file.CreateHeader(FORMAT_VERSION).AsMemory(), cancellation);
		return (writer, file);
	}

	protected override void WriteLine((StreamWriter Writer, CpsFile File) state, CpsFile.SongLine? line)
	{
		if (line is null)
		{
			state.Writer.WriteLine();
			return;
		}

		var builder = new StringBuilder();
		line.Write(builder, state.File.TokenConfiguration);
		state.Writer.WriteLine(builder.ToString());
	}

	protected override Task WriteLineAsync((StreamWriter Writer, CpsFile File) state, CpsFile.SongLine? line, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		if (line is null)
			return state.Writer.WriteLineAsync();

		var builder = new StringBuilder();
		line.Write(builder, state.File.TokenConfiguration);
		cancellation.ThrowIfCancellationRequested();
		return state.Writer.WriteLineAsync(builder, cancellation);
	}

	protected override void Finalize((StreamWriter Writer, CpsFile File) state, CpsSheetEncoder encoder)
	{
		state.Writer.Flush();
		state.Writer.Dispose();
	}

	protected override async Task FinalizeAsync((StreamWriter Writer, CpsFile File) state, CpsSheetEncoder encoder, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		await state.Writer.FlushAsync(cancellation);
		cancellation.ThrowIfCancellationRequested();
		await state.Writer.DisposeAsync();
	}
}
