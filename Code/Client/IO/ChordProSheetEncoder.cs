using Konves.ChordPro;
using Konves.ChordPro.Directives;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public class ChordProSheetEncoder : SheetEncoderBase<ILine>
{
	public ChordProSheetEncoder(ISheetBuilderFormatter? formatter = null)
		: base(formatter ?? DefaultSheetEncoder.DefaultWriterFormatter.Instance)
	{ }

	public override IEnumerable<ILine?> ProcessLines(SheetDocument document)
	{
		ChordProConfiguration.StartOfSectionDirective? currentSectionDirective = null;

		foreach (var sheetLine in document.Lines.GetLinesWithContext())
		{
			if (sheetLine.Line is SheetEmptyLine)
			{
				yield return new SongLine();
				continue;
			}

			if (sheetLine.Line is SheetTabLine tabLine)
			{
				yield return new StartOfTabDirective();
				foreach (var displayLine in sheetLine.CreateDisplayLines(Formatter))
				{
					var lineText = string.Join(null, displayLine.GetElements().Select(e => e.ToString()));
					yield return new TabLine(lineText);
				}
				yield return new EndOfTabDirective();
				continue;
			}

			if (sheetLine.Line is SheetVarietyLine varietyLine)
			{
				if (varietyLine.IsTitleLine(out var title))
				{
					if (currentSectionDirective is not null)
						yield return currentSectionDirective.CreateEnd();

					if (title.Contains("Str", StringComparison.InvariantCultureIgnoreCase) || title.Contains("Vers", StringComparison.InvariantCultureIgnoreCase))
						yield return currentSectionDirective = ChordProConfiguration.Default.StartVerse(title);
					else if (title.Contains("Bridge", StringComparison.InvariantCultureIgnoreCase))
						yield return currentSectionDirective = ChordProConfiguration.Default.StartBridge(title);
					else
						yield return currentSectionDirective = ChordProConfiguration.Default.StartChorus(title);

					continue;
				}

				var blocks = new List<Block>();
				foreach (var component in varietyLine.GetComponents().Index())
					if (component.Item is SheetVarietyLine.VarietyComponent varietyComponent)
						switch (varietyComponent.Content.Type)
						{
							case SheetVarietyLine.ContentType.Chord:
								var chord = new Konves.ChordPro.Chord(varietyComponent.Content.ToString(Formatter));
								blocks.Add(chord);
								break;

							case SheetVarietyLine.ContentType.Space:
								var spaceText = varietyComponent.Content.Content.ToString();
								var spaceLength = spaceText.Length;
								if (spaceLength <= 0)
									break;

								blocks.Add(new Whitespace(spaceLength));
								break;

							default:
								var wordText = varietyComponent.Content.ToString(Formatter);
								var syllables = new List<Syllable>();
								SheetVarietyLine.VarietyComponent.Attachment? previousAttachment = null;
								foreach (var attachment in varietyComponent.Attachments.Append(null))
								{
									if (previousAttachment is null)
									{
										if (attachment is null)
											syllables.Add(new(null, wordText));
										else if (attachment.Offset != ContentOffset.Zero)
											syllables.Add(new(null, wordText[..attachment.Offset.Value]));
									}
									else if (previousAttachment is SheetVarietyLine.VarietyComponent.VarietyAttachment varietyAttachment)
									{
										var textBetween = attachment is null
											? wordText[previousAttachment.Offset.Value..]
											: wordText[previousAttachment.Offset.Value..attachment.Offset.Value];

										var attachmentText = varietyAttachment.Content.ToString(Formatter);
										if (varietyAttachment.Content.Type != SheetVarietyLine.ContentType.Chord)
											attachmentText = "*" + attachmentText;

										syllables.Add(new(new Konves.ChordPro.Chord(attachmentText), textBetween));
									}
									else
										throw new SheetWriterException("Unbekannter Attachmenttyp: " + previousAttachment.GetType().Name);

									previousAttachment = attachment;
								}

								var word = new Word(syllables);
								blocks.Add(word);
								break;
						}
					else
						throw new SheetWriterException("Unbekannter Komponententyp: " + component.GetType().Name);

				yield return new SongLine(blocks);
				continue;
			}

			throw new SheetWriterException("Unbekannter Zeilentyp: " + sheetLine.GetType().Name);
		}

		if (currentSectionDirective is not null)
		{
			yield return currentSectionDirective.CreateEnd();
			currentSectionDirective = null;
		}
	}
}

public class ChordProSheetEncoderWriter : SheetEncoderWriter<ILine, ChordProSheetEncoder, (Stream stream, Document document, bool leaveOpen)>
{
	public static new ChordProSheetEncoderWriter Default { get; } = new();

	protected override ChordProSheetEncoder GetEncoder() => new ChordProSheetEncoder();

	protected override (Stream stream, Document document, bool leaveOpen) Open(Stream stream, bool leaveOpen = false)
		=> (stream, new(new List<ILine>()), leaveOpen);
	protected override Task<(Stream stream, Document document, bool leaveOpen)> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
		=> Task.FromResult(Open(stream, leaveOpen));

	protected override void WriteLine((Stream stream, Document document, bool leaveOpen) state, ILine? line) => state.document.Lines.Add(line);
	protected override Task WriteLineAsync((Stream stream, Document document, bool leaveOpen) state, ILine? line, CancellationToken cancellation = default)
	{
		WriteLine(state, line);
		return Task.CompletedTask;
	}

	protected override void Finalize((Stream stream, Document document, bool leaveOpen) state, ChordProSheetEncoder encoder)
	{
		using (var writer = new StreamWriter(state.stream, leaveOpen: state.leaveOpen))
			ChordProSerializer.Serialize(state.document, writer, new SerializerSettings()
			{
				CustomHandlers = ChordProConfiguration.Default.DirectiveHandlers.ToList()
			});
	}

	protected override async Task FinalizeAsync((Stream stream, Document document, bool leaveOpen) state, ChordProSheetEncoder encoder, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		using (var ms = new MemoryStream())
		{
			Finalize((ms, state.document, state.leaveOpen), encoder);

			ms.Position = 0;
			cancellation.ThrowIfCancellationRequested();
			await ms.CopyToAsync(state.stream, cancellation);
		}
	}
}