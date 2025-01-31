using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;
using Skinnix.RhymeTool.Data.Notation.Display.Caching;
using static System.Net.Mime.MediaTypeNames;

namespace Skinnix.RhymeTool.Data.Notation;

public partial class SheetVarietyLine : SheetLine, ISelectableSheetLine, ISheetTitleLine
{
	public const char TITLE_START_DELIMITER = '#';
	public const char TITLE_END_DELIMITER = '#';

	public static SheetLineType LineType { get; } = SheetLineType.Create<SheetVarietyLine>("Text");

	public event EventHandler? IsTitleLineChanged;

	private readonly ComponentCollection components;
	private readonly ContentEditing contentEditor;
	private readonly AttachmentEditing attachmentEditor;

	private ISheetBuilderFormatter? cachedFormatter;
	private IEnumerable<SheetDisplayLine>? cachedLines;
	private string? cachedTitle;

	public ISheetDisplayLineEditing ContentEditor => contentEditor;
	public ISheetDisplayLineEditing AttachmentEditor => attachmentEditor;

	public int TextLineId => contentEditor.LineId;
	public int AttachmentLineId => attachmentEditor.LineId;

	public override bool IsEmpty => components.Count == 0;

	public SheetVarietyLine()
		: base(LineType)
	{
		components = new(this);

		contentEditor = new(this);
		attachmentEditor = new(this);
	}

	public SheetVarietyLine(IEnumerable<Component> components)
		: base(LineType)
	{
		this.components = new(this, components);

		contentEditor = new(this);
		attachmentEditor = new(this);
	}

	public override IEnumerable<SheetLineConversion> GetPossibleConversions(ISheetBuilderFormatter? formatter = null)
	{
		if (!IsEmpty)
			return [];

		return [SheetLineConversion.Simple<SheetTabLine>.Instance];
	}

	public IEnumerable<Component> GetComponents() => components;

	#region Display
	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(SheetLineContext context, ISheetBuilderFormatter? formatter = null)
	{
		//Prüfe Cache
		if (cachedFormatter == formatter && cachedLines is not null)
			return cachedLines;

		//Erzeuge Cache
		cachedLines = CreateDisplayLinesCore(formatter).ToList();

		//Speichere Formatter
		if (cachedFormatter is IModifiable modifiableCachedFormatter)
			modifiableCachedFormatter.Modified -= OnCachedFormatterModified;
		cachedFormatter = formatter;
		if (cachedFormatter is IModifiable modifiableFormatter)
			modifiableFormatter.Modified += OnCachedFormatterModified;
		return cachedLines;
	}

	private void OnCachedFormatterModified(object? sender, ModifiedEventArgs e) => InvalidateCache();

	private void InvalidateCache()
	{
		cachedLines = null;
		if (cachedFormatter is IModifiable modifiableCachedFormatter)
			modifiableCachedFormatter.Modified -= OnCachedFormatterModified;
		cachedFormatter = null;
	}

	private void RaiseModifiedAndInvalidateCache()
	{
		InvalidateCache();

		//Hat sich der Titel oder Titelstatus geändert?
		IsTitleLine(out var title);
		if (title != cachedTitle)
		{
			cachedTitle = title;
			IsTitleLineChanged?.Invoke(this, EventArgs.Empty);
		}
		
		RaiseModified(new ModifiedEventArgs(this));
	}

	private IEnumerable<SheetDisplayLine> CreateDisplayLinesCore(ISheetBuilderFormatter? formatter)
	{
		//Erzeuge Builder
		var builders = new Component.LineBuilders(this,
			new SheetDisplayTextLine.Builder(),
			new SheetDisplayChordLine.Builder())
		{
			IsTitleLine = IsTitleLine(out _),
		};

		//Gehe durch alle Komponenten
		var componentIndex = 0;
		foreach (var component in components)
			component.BuildLines(builders, componentIndex++, formatter);

		//Sind alle Zeilen leer?
		if (builders.CurrentLength == 0)
		{
			yield return new SheetDisplayEmptyLine(0) { Editing = contentEditor };
			yield break;
		}

		//Nachbearbeitung der Zeilen
		formatter?.AfterPopulateLine(this, builders.TextLine, builders.AllLines);
		formatter?.AfterPopulateLine(this, builders.ChordLine, builders.AllLines);

		//Zeige Akkordzeile
		if (formatter?.ShowLine(this, builders.ChordLine) != false)
			yield return builders.ChordLine.CreateDisplayLine(1, attachmentEditor);

		//Zeige Textzeile
		if (formatter?.ShowLine(this, builders.TextLine) != false)
			yield return builders.TextLine.CreateDisplayLine(0, contentEditor);
	}
	#endregion

	#region Title
	public bool IsTitleLine([NotNullWhen(true)] out string? title)
		=> CheckTitleLine(out title, out _, out _);

	private bool CheckTitleLine([NotNullWhen(true)] out string? title, out IReadOnlyList<VarietyComponent> titleComponents, out int afterTitleIndex)
	{
		//Alles, was von Klammern umschlossen ist und keine Attachments hat, ist es ein Titel
		var titleBuilder = new StringBuilder();
		var titleComponentsList = new List<VarietyComponent>();
		titleComponents = titleComponentsList;
		afterTitleIndex = 0;
		var i = -1;
		foreach (var component in components)
		{
			i++;

			//Ist die Komponente keine VarietyComponent, kein Text oder hat Attachments?
			if (component is not VarietyComponent variety || !variety.Content.Content.Is<string>(out var text) || variety.Attachments.Count != 0)
			{
				title = null;
				afterTitleIndex = 0;
				return false;
			}

			//Erste Komponente muss mit delimiter beginnen
			if (i == 0 && !text.StartsWith(TITLE_START_DELIMITER))
			{
				title = null;
				afterTitleIndex = 0;
				return false;
			}

			//Baue den Titel zusammen
			titleBuilder.Append(text);
			titleComponentsList.Add(variety);
			if (text.EndsWith(TITLE_END_DELIMITER))
			{
				//Titel gefunden
				title = titleBuilder.ToString(1, titleBuilder.Length - 2);
				afterTitleIndex = i + 1;
				return true;
			}
		}

		if (titleBuilder.Length == 0)
		{
			title = null;
			return false;
		}

		title = titleBuilder.ToString();
		afterTitleIndex = i;
		return true;
	}
	#endregion

	#region Content
	public enum ContentType
	{
		Word,
		Space,
		Punctuation,
		Chord,
		Fingering,
		Rhythm,
	}

	[Flags]
	public enum SpecialContentType
	{
		None = 0,
		Text = 1,
		Chord = 2,
		Fingering = 4,
		Rhythm = 8,

		All = Text | Chord | Fingering | Rhythm,
	}

	internal readonly record struct MergeResult(ComponentContent NewContent, ContentOffset LengthBefore)
	{
		public ContentOffset MergeLengthBefore { get; init; }
	}

	internal readonly struct ComponentContent
	{
		public const string PUNCTUATION = ",.-><!?\"";

		public Either<string, Chord, Fingering, RhythmPattern> Content { get; }

		public bool IsEmpty => Content.Value is null || (Content.Value is string s && string.IsNullOrEmpty(s));

		public ContentType Type => Content.Switch(
			s => string.IsNullOrWhiteSpace(s) ? ContentType.Space : s.All(PUNCTUATION.Contains) ? ContentType.Punctuation : ContentType.Word,
			_ => ContentType.Chord,
			_ => ContentType.Fingering,
			_ => ContentType.Rhythm);

		public ComponentContent(string text)
		{
			Content = text;
		}

		public ComponentContent(Chord chord)
		{
			Content = chord;
		}

		public ComponentContent(Fingering fingering)
		{
			Content = fingering;
		}

		public ComponentContent(RhythmPattern rhythm)
		{
			Content = rhythm;
		}

		public static ComponentContent FromString(string content, ISheetEditorFormatter? formatter, SpecialContentType allowedTypes = SpecialContentType.All)
		{
			if ((allowedTypes & SpecialContentType.Chord) != 0)
			{
				//Versuche den Inhalt als Akkord zu lesen
				var chordLength = Chord.TryRead(formatter, content, out var chord);
				if (chord is not null && chordLength == content.Length)
				{
					//Der Inhalt ist ein Akkord
					return new(chord);
				}
			}

			if ((allowedTypes & SpecialContentType.Fingering) != 0)
			{
				//Versuche den Inhalt als Fingering zu lesen
				var fingeringLength = Fingering.TryRead(formatter, content, out var fingering);
				if (fingering is not null && fingeringLength == content.Length)
				{
					//Der Inhalt ist ein Fingering
					return new(fingering);
				}
			}

			if ((allowedTypes & SpecialContentType.Rhythm) != 0)
			{
				//Versuche den Inhalt als Rhythmus zu lesen
				var rhythmLength = RhythmPattern.TryRead(formatter, content, out var rhythm);
				if (rhythm is not null && rhythmLength == content.Length)
				{
					//Der Inhalt ist ein Rhythmus
					return new(rhythm);
				}
			}

			//Der Inhalt ist kein Akkord
			return new(content);
		}

		public static int TryRead(ReadOnlySpan<char> content, out ComponentContent? result, ISheetEditorFormatter? formatter)
		{
			if (content.Length == 0)
			{
				result = null;
				return -1;
			}

			//Prüfe auf Akkord
			var read = Chord.TryRead(formatter, content, out var chord);
			if (read > 0 && chord is not null)
			{
				result = new ComponentContent(chord);
				return read;
			}

			//Prüfe auf Fingering
			read = Fingering.TryRead(formatter, content, out var fingering);
			if (read > 0 && fingering is not null)
			{
				result = new ComponentContent(fingering);
				return read;
			}

			//Prüfe auf Rhythmus
			read = RhythmPattern.TryRead(formatter, content, out var rhythm);
			if (read > 0 && rhythm is not null)
			{
				result = new ComponentContent(rhythm);
				return read;
			}

			//Prüfe auf Leerzeichen
			if (char.IsWhiteSpace(content[0]))
			{
				for (read = 1; read < content.Length && char.IsWhiteSpace(content[read]); read++) ;

				result = new ComponentContent(new string(content[0..read]));
				return read;
			}

			//Prüfe auf Interpunktion
			if (PUNCTUATION.Contains(content[0]))
			{
				for (read = 1; read < content.Length && PUNCTUATION.Contains(content[read]); read++) ;

				result = new ComponentContent(new string(content[0..read]));
				return read;
			}

			//Lese Wort
			for (read = 1; read < content.Length && !char.IsWhiteSpace(content[read]) && !PUNCTUATION.Contains(content[read]); read++) ;
			result = new ComponentContent(new string(content[0..read]));
			return read;
		}

		public static ComponentContent CreateSpace(ContentOffset length, ISheetEditorFormatter? formatter)
			=> new ComponentContent(new string(' ', length.Value));

		public SheetDisplayLineElement CreateElement(SheetDisplaySliceInfo sliceInfo, ISheetFormatter? formatter) => Content.Switch<SheetDisplayLineElement>(
			text => string.IsNullOrWhiteSpace(text)
				? new SheetDisplayLineSpace(text?.Length ?? 0) { Slice = sliceInfo }
				: new SheetDisplayLineText(text) { Slice = sliceInfo },
			chord => new SheetDisplayLineChord(chord, chord.Format(formatter)) { Slice = sliceInfo },
			fingering => new SheetDisplayLineFingering(fingering, fingering.Format(formatter)) { Slice = sliceInfo },
			rhythm => new SheetDisplayLineRhythmPattern(rhythm, rhythm.Format(formatter)) { Slice = sliceInfo }
		);

		public SheetDisplayLineElement CreateDisplayElementPart(SheetDisplaySliceInfo sliceId, ContentOffset offset, ContentOffset length, ISheetBuilderFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = ToString(formatter);
			if (offset == ContentOffset.Zero && length.Value >= textContent.Length)
				return CreateElement(sliceId, formatter);

			//Bilde Substring
			var subContent = new ComponentContent(
				textContent.Substring(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value)));
			return subContent.CreateElement(sliceId, formatter);
		}

		public ContentOffset GetLength(ISheetFormatter? formatter)
			=> new(ToString(formatter).Length);

		internal ComponentContent? RemoveContent(ContentOffset offset, ContentOffset length, SpecialContentType allowedTypes, ISheetEditorFormatter? formatter)
		{
			if (offset < ContentOffset.Zero)
			{
				length -= offset;
				offset = ContentOffset.Zero;
			}

			if (length <= ContentOffset.Zero)
				return null;

			//Textinhalt
			var textContent = ToString(formatter);
			if (textContent is null) return null;

			//Kürze den Textinhalt
			if (offset.Value >= textContent.Length) return null;
			var newContent = textContent.Remove(offset.Value, Math.Min(length.Value, textContent.Length - offset.Value));
			if (newContent == textContent) return null;

			//Setze den neuen Inhalt
			return FromString(newContent, formatter, allowedTypes);
		}

		internal MergeResult MergeContents(string content, ContentOffset offset, SpecialContentType allowedTypes, ISheetEditorFormatter? formatter)
		{
			//Textinhalt
			var textContent = ToString(formatter);

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset.Value, textContent.Length);
			var newContent = textContent[0..stringOffset] + content;
			if (offset.Value < textContent.Length)
				newContent += textContent[stringOffset..];

			//Ergebnis
			return new(FromString(newContent, formatter, allowedTypes), new ContentOffset(textContent.Length))
			{
				MergeLengthBefore = new ContentOffset(content.Length)
			};
		}

		internal MergeResult MergeContents(ComponentContent content, ContentOffset offset, SpecialContentType allowedTypes, ISheetEditorFormatter? formatter)
		{
			//Textinhalt
			var textContent = ToString(formatter);
			var lengthBefore = textContent.Length;
			var afterTextContent = content.ToString(formatter);
			var stringOffset = Math.Min(offset.Value, textContent.Length);

			//Sonderfall: Wird ein Text hinten an einen Akkord angefügt?
			if (Content.Is<Chord>(out var chord) && content.Type is ContentType.Word && stringOffset >= textContent.Length)
			{
				//Verwende den ursprünglichen Text des Akkords
				textContent = chord.OriginalText;
				stringOffset = textContent.Length;
			}

			//Füge den Textinhalt hinzu
			var newContent = textContent[0..stringOffset] + afterTextContent;
			if (offset.Value < textContent.Length)
				newContent += textContent[stringOffset..];

			//Ergebnis
			return new(FromString(newContent, formatter, allowedTypes), new ContentOffset(lengthBefore))
			{
				MergeLengthBefore = new ContentOffset(afterTextContent?.Length ?? 0)
			};
		}

		internal (ComponentContent NewContent, ComponentContent EndContent)? SplitEnd(ContentOffset offset, SpecialContentType allowedTypes, SpecialContentType endAllowedTypes, ISheetEditorFormatter? formatter)
		{
			//Textinhalt
			var textContent = ToString(formatter);

			if (offset.Value >= textContent.Length)
				return null;

			//Trenne den Textinhalt auf
			var newContent = textContent[..offset.Value];
			var newEndContent = textContent[offset.Value..];

			//Erzeuge das neue Ende
			return (
				FromString(newContent, formatter, allowedTypes),
				FromString(newEndContent, formatter, endAllowedTypes));
		}

		#region Operators
		public override string ToString() => ToString(null);
		public string ToString(ISheetFormatter? formatter) => Content.Switch(
			text => text,
			chord => chord.ToString(formatter),
			fingering => fingering.ToString(formatter),
			rhythm => rhythm.ToString(formatter));
		#endregion
	}
	#endregion
}
