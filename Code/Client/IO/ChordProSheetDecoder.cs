using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Konves.ChordPro;
using Konves.ChordPro.Directives;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.IO;

public class ChordProSheetDecoder : SheetDecoderBase<ILine>
{
	private readonly List<SheetLine> lines = new();

	private List<(Note? Tuning, Queue<SheetDecoderHelper.TabLineElement> Elements)>? currentTab = null;

	public string ChorusTitle { get; set; } = "Chorus";

	public ChordProSheetDecoder(ISheetEditorFormatter formatter)
		: base(formatter)
	{ }

	public ChordProSheetDecoder()
		: base(DefaultSheetFormatter.Instance)
	{ }

	public override void ProcessLine(ILine line)
	{
		switch (line)
		{
			case SongLine songLine:
				lines.AddRange(Read(songLine));
				break;
			case TabLine tabLine:
				lines.AddRange(Read(tabLine));
				break;
			case Directive directive:
				lines.AddRange(Read(directive));
				break;
		}
	}

	public override SheetDocument Finalize() => new(lines);

	protected virtual IEnumerable<SheetLine> Read(SongLine songLine)
	{
		var components = new List<SheetVarietyLine.Component>();
		var needsSpace = false;
		foreach (var block in songLine.Blocks)
			switch (block)
			{
				case Konves.ChordPro.Chord chord:
					if (chord.Text is null || Data.Notation.Chord.TryRead(Formatter, chord.Text, out var sheetChord) != chord.Text.Length || sheetChord is null)
						throw new SheetReaderException("Unbekannter Akkord: " + chord.Text);

					if (needsSpace)
						components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(ContentOffset.One, Formatter));

					components.Add(new SheetVarietyLine.VarietyComponent(sheetChord));
					needsSpace = true;
					break;

				case Whitespace whitespace:
					var whitespaceLength = whitespace.Length;

					if (needsSpace)
						whitespaceLength++;

					components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(new(whitespaceLength), Formatter));
					needsSpace = false;
					break;

				case Word word:
					var fullWord = string.Join(null, word.Syllables.Select(s => s?.Text));
					var component = new SheetVarietyLine.VarietyComponent(fullWord);

					var offset = 0;
					foreach (var syllable in word.Syllables)
					{
						if (syllable.Chord is not null)
						{
							var attachmentContent = syllable.Chord.Text.StartsWith('*')
								? SheetVarietyLine.ComponentContent.FromString(syllable.Chord.Text[1..], Formatter, SheetVarietyLine.SpecialContentType.Text)
								: SheetVarietyLine.ComponentContent.FromString(syllable.Chord.Text, Formatter);
							component.AddAttachment(new SheetVarietyLine.VarietyComponent.VarietyAttachment(new ContentOffset(offset), attachmentContent));
						}

						offset += syllable.Text?.Length ?? 0;
					}

					if (needsSpace)
						components.Add(SheetVarietyLine.VarietyComponent.CreateSpace(ContentOffset.One, Formatter));

					components.Add(component);
					needsSpace = true;
					break;
				default:
					throw new SheetReaderException("Unbekannter Blocktyp: " + block.GetType().Name);
			}

		if (components.Count == 0)
			return [new SheetEmptyLine()];

		return [new SheetVarietyLine(components)];
	}

	protected virtual IEnumerable<SheetLine> Read(TabLine tabLine)
	{
		if (currentTab is null)
			throw new SheetReaderException("Tabulaturzeile ohne Start der Tabulatur");

		var text = tabLine.Text.Trim().AsSpan();
		if (text.Length == 0)
			return [];

		for (var offset = 0; offset < tabLine.Text.Length / 2; offset++)
			for (var cutoff = 0; offset + cutoff < tabLine.Text.Length / 2; cutoff++)
			{
				var tabLineRead = SheetDecoderHelper.TryParseTabLine(text[offset..^cutoff], Formatter);
				if (tabLineRead is not null)
				{
					currentTab.Add(tabLineRead.Value);

					if (offset == 0)
						return [];

					//Prüfe Text davor
					var padding = text[..offset].Trim();
					if (padding.Length == 0)
						return [];

					//Füge Text davor als Zeile hinzu
					return [new SheetVarietyLine([new SheetVarietyLine.VarietyComponent(SheetVarietyLine.ComponentContent.FromString(new string(padding), Formatter))])];
				}
			}

		throw new SheetReaderException("Ungültige Tabulaturzeile: " + tabLine.Text);
	}

	protected virtual IEnumerable<SheetLine> Read(Directive directive)
	{
		switch (directive)
		{
			case StartOfTabDirective:
				currentTab = new();
				return [];
			case EndOfTabDirective:
				if (currentTab is null)
					throw new SheetReaderException("Ende der Tabulatur ohne Start der Tabulatur");

				var tabLine = SheetDecoderHelper.CreateTabLine(currentTab);
				currentTab = null;
				return [tabLine];

			case ChordProConfiguration.StartOfSectionDirective sectionStart:
				var title = sectionStart.Label ?? sectionStart.SectionKey.ToUpperFirst();
				var titleLine = new SheetVarietyLine([new SheetVarietyLine.VarietyComponent(SheetVarietyLine.TITLE_START_DELIMITER + title)]);
				return [titleLine];
			case ChordProConfiguration.EndOfSectionDirective:
				return [];

			case StartOfChorusDirective:
				titleLine = new SheetVarietyLine([new SheetVarietyLine.VarietyComponent(SheetVarietyLine.TITLE_START_DELIMITER + ChorusTitle)]);
				return [titleLine];
			case EndOfChorusDirective:
				return [];

			case CommentDirective:
			case CommentItalicDirective:
				var commentText = ((directive as CommentDirective)?.Text ?? (directive as CommentItalicDirective)?.Text)?.Trim();
				if (commentText?.StartsWith('(') != true)
					commentText = $"({commentText}";
				if (!commentText.EndsWith(')'))
					commentText = $"{commentText})";

				return [new SheetVarietyLine([new SheetVarietyLine.VarietyComponent(commentText)])];

			default:
				return [];
		}
	}
}

public class ChordProSheetDecoderReader : SheetDecoderReader<ILine, ChordProSheetDecoder, IEnumerator<ILine>>
{
	public static new ChordProSheetDecoderReader Default { get; } = new();

	protected override ChordProSheetDecoder GetDecoder() => new();

	protected override IEnumerator<ILine> Open(Stream stream, bool leaveOpen = false)
		=> ChordProSerializer.Deserialize(stream, ChordProConfiguration.Default.DirectiveHandlers).Lines.GetEnumerator();

	protected override async Task<IEnumerator<ILine>> OpenAsync(Stream stream, bool leaveOpen = false, CancellationToken cancellation = default)
	{
		using (var ms = new MemoryStream())
		{
			await stream.CopyToAsync(ms, cancellation);
			ms.Position = 0;

			return Open(ms);
		}
	}

	protected override ILine? TryReadLine(IEnumerator<ILine> state)
		=> state.MoveNext() ? state.Current : null;

	protected override Task<ILine?> TryReadLineAsync(IEnumerator<ILine> state, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		return Task.FromResult(state.MoveNext() ? state.Current : null);
	}
}
