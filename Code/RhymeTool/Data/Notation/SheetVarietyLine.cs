using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetVarietyLine : SheetLine, ISheetTitleLine
{
	private readonly List<Component> components;
	private readonly ContentEditing contentEditing;
	private readonly AttachmentEditing attachmentEditing;

	public SheetVarietyLine()
	{
		components = new();

		contentEditing = new(this);
		attachmentEditing = new(this);
	}

	public SheetVarietyLine(IEnumerable<Component> components)
	{
		this.components = new(components);

		contentEditing = new(this);
		attachmentEditing = new(this);
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
	{
		//Erzeuge Builder
		var builders = new Component.LineBuilders(this,
			new SheetDisplayTextLine.Builder(),
			new SheetDisplayChordLine.Builder());

		//Sonderfall: Titel?
		if (IsTitleLine(out var title, out var afterTitleIndex))
		{
			//Füge Titel hinzu
			builders.TextLine.Append(new SheetDisplayLineSegmentTitleBracket("["));
			if (title is not null)
				builders.TextLine.Append(new SheetDisplayLineSegmentTitleText(title));
			builders.TextLine.Append(new SheetDisplayLineSegmentTitleBracket("]"));

			//Füge Komponenten nach dem Titel hinzu
			for (var i = afterTitleIndex; i < components.Count; i++)
				components[i].BuildLines(builders, formatter);
		}
		else
		{
			//Gehe durch alle Komponenten
			foreach (var component in components)
				component.BuildLines(builders, formatter);
		}

		//Sind alle Zeilen leer?
		if (builders.CurrentLength == 0)
		{
			yield return new SheetDisplayEmptyLine() { Editing = contentEditing };
			yield break;
		}

		//Gib nichtleere Zeilen zurück
		if (builders.ChordLine.CurrentLength > 0)
			yield return builders.ChordLine.CreateDisplayLine(attachmentEditing);
		if (builders.TextLine.CurrentLength > 0)
			yield return builders.TextLine.CreateDisplayLine(contentEditing);
	}

	#region Title
	public bool IsTitleLine(out string? title)
		=> IsTitleLine(out title, out _);

	public bool IsTitleLine(out string? title, out int afterTitleIndex)
	{
		//Alles, was von Klammern umschlossen ist und keine Attachments hat, ist es ein Titel
		var titleBuilder = new StringBuilder();
		var i = -1;
		foreach (var component in components)
		{
			i++;

			//Ist die Komponente keine VarietyComponent, kein Text oder hat Attachments?
			if (component is not VarietyComponent variety || variety.Content.Text is null || variety.Attachments.Count != 0)
			{
				title = null;
				afterTitleIndex = 0;
				return false;
			}

			//Erste Komponente muss mit öffnender Klammer beginnen
			if (i == 0 && !variety.Content.Text.StartsWith('['))
			{
				title = null;
				afterTitleIndex = 0;
				return false;
			}

			//Baue den Titel zusammen
			titleBuilder.Append(variety.Content.Text);
			if (variety.Content.Text.EndsWith(']'))
			{
				//Titel gefunden
				title = titleBuilder.ToString(1, titleBuilder.Length - 2);
				afterTitleIndex = i + 1;
				return true;
			}
		}

		title = null;
		afterTitleIndex = 0;
		return false;
	}
	#endregion

	#region Editing
	private class ContentEditing : ISheetDisplayLineEditing
	{
		private readonly SheetVarietyLine owner;

		public ContentEditing(SheetVarietyLine owner)
		{
			this.owner = owner;
		}

		public LineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false)
		{
			//Ist der Bereich leer?
			if (selectionRange.Length == 0)
			{
				if (forward)
					selectionRange = new SimpleRange(selectionRange.Start, selectionRange.Start + 1);
				else
					selectionRange = new SimpleRange(selectionRange.Start - 1, selectionRange.Start);
			}

			return DeleteAndInsertContent(selectionRange, formatter, null);
		}

		public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter)
		{
			return DeleteAndInsertContent(selectionRange, formatter, content);
		}

		private LineEditResult DeleteAndInsertContent(SimpleRange selectionRange, ISheetFormatter? formatter, string? content)
		{
			//Finde alle Komponente im Bereich
			Component? before = null;
			int beforeIndex = -1;
			Component? after = null;
			int afterIndex = -1;
			List<Component> fullyInside = new();
			var rangeStartsOnComponent = false;
			var index = -1;
			foreach (var component in owner.components)
			{
				//Berechne Länge der Komponente
				index++;
				var bounds = component.ContentRenderBounds;

				//Liegt die Komponente komplett vor dem Bereich?
				if (bounds.EndOffset < selectionRange.Start)
					continue;

				//Liegt die Komponente komplett hinter dem Bereich?
				if (bounds.StartOffset > selectionRange.End)
					break;

				//Beginnt die Komponente vor dem Bereich?
				if (before is null && bounds.StartOffset < selectionRange.Start)
				{
					//Die Komponente ist der linke Rand
					before = component;
					beforeIndex = index;
				}

				//Beginnt die Komponente mit dem Bereich?
				if (bounds.StartOffset == selectionRange.Start)
					rangeStartsOnComponent = true;

				//Endet die Komponente nach dem Bereich?
				if (after is null && bounds.EndOffset > selectionRange.End)
				{
					//Die Komponente ist der rechte Rand
					after = component;
					afterIndex = index;
				}

				//Liegt die Komponente komplett im Bereich?
				if (component != before && component != after
					&& bounds.StartOffset >= selectionRange.Start && bounds.EndOffset <= selectionRange.End)
				{
					//Die Komponente liegt im Bereich
					fullyInside.Add(component);
				}
			}

			//Prüfe auf Änderungen
			var removedAnything = fullyInside.Count != 0;
			var addedContentLength = content?.Length ?? 0;

			//Entferne Überlappung am linken Rand
			List<VarietyComponent>? newContentComponents = null;
			var skipTrimAfter = false;
			if (before is not null)
			{
				//Kürze die Komponente
				var tailLength = selectionRange.Start - before.ContentRenderBounds.StartOffset;
				if (before.RemoveContent(tailLength, selectionRange.Length, formatter))
					removedAnything = true;

				//Gibt es einen Inhalt?
				if (content is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					var repeat = false;
					do
					{
						//Sind linker und rechter Rand gleich und der Inhalt kann nicht einfach so hinzugefügt werden?
						if (after == before && (newContentComponents.Count > 1 || repeat))
						{
							//Teile die Randkomponente in zwei Teile
							var newAfter = before.SplitEnd(tailLength, formatter);

							//Füge den neuen rechten Rand ein
							if (beforeIndex == owner.components.Count - 1)
								owner.components.Add(newAfter);
							else
								owner.components.Insert(beforeIndex + 1, newAfter);

							//Der rechte Rand muss jetzt nicht mehr gekürzt werden
							after = newAfter;
							skipTrimAfter = true;
						}

						//Versuche die erste Komponente hinten an den linken Rand anzufügen
						if (before.TryMerge(newContentComponents[0], tailLength, formatter))
						{
							//Erste Komponente hinzugefügt
							newContentComponents.RemoveAt(0);

							//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
							if (newContentComponents.Count > 0 && after?.TryMerge(newContentComponents[^1], 0, formatter) == true)
							{
								//Letzte Komponente hinzugefügt
								newContentComponents.RemoveAt(newContentComponents.Count - 1);
							}

							//Füge die restlichen Komponenten dazwischen ein
							if (newContentComponents.Count > 0)
							{
								if (beforeIndex == owner.components.Count - 1)
									owner.components.AddRange(newContentComponents);
								else
									owner.components.InsertRange(beforeIndex + 1, newContentComponents);
							}

							//Anfügen erfolgreich
							content = null;
						}
						else
						{
							//Der Rand muss aufgetrennt werden
							repeat = true;
						}
					}
					while (before == after && repeat);
				}
			}

			//Gibt es einen Inhalt und beginnt der Bereich mit einer Komponente?
			if (content is not null && rangeStartsOnComponent)
			{
				//Versuche den Inhalt an den Anfang in diese Komponente anzufügen
				var firstFullyInside = fullyInside.FirstOrDefault();
				if (firstFullyInside is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					//Besteht der Inhalt nur aus einer Komponente?
					if (newContentComponents.Count == 1)
					{
						//Versuche die Komponente vorne an die erste innere anzufügen
						if (firstFullyInside.TryMerge(newContentComponents[0], 0, formatter))
						{
							//Kürze den Inhalt der ersten Komponente
							firstFullyInside.RemoveContent(content.Length, int.MaxValue, formatter);

							//Entferne die Komponente doch nicht
							fullyInside.RemoveAt(0);

							//Anfügen erfolgreich
							content = null;
						}
					}
				}
			}

			//Entferne Überlappung am rechten Rand
			if (after is not null && after != before)
			{
				//Kürze die Komponente
				var overlap = selectionRange.End - after.ContentRenderBounds.StartOffset;
				if (!skipTrimAfter && after.RemoveContent(0, overlap, formatter))
					removedAnything = true;

				//Gibt es einen Inhalt?
				if (content is not null)
				{
					//Erzeuge Komponenten für den Inhalt
					newContentComponents ??= CreateComponentsForContent(content);

					//Versuche die letzte Komponente vorne an den rechten Rand anzufügen
					if (after.TryMerge(newContentComponents[^1], 0, formatter))
					{
						//Erste Komponente hinzugefügt
						newContentComponents.RemoveAt(newContentComponents.Count - 1);

						//Füge die restlichen Komponenten davor ein
						if (newContentComponents.Count > 0)
						{
							owner.components.InsertRange(afterIndex, newContentComponents);
						}

						//Anfügen erfolgreich
						content = null;
					}
				}
			}

			//Entferne alle Komponenten, die komplett im Bereich liegen
			owner.components.RemoveAll(fullyInside.Contains);

			//Wurde der Inhalt immer noch nicht hinzugefügt?
			if (content is not null)
			{
				//Erzeuge Komponenten für den Inhalt
				newContentComponents ??= CreateComponentsForContent(content);

				//Füge die Komponenten nach dem linken Rand ein
				if (beforeIndex == owner.components.Count - 1)
					owner.components.AddRange(newContentComponents);
				else
					owner.components.InsertRange(beforeIndex + 1, newContentComponents);

				//Hinzufügen erfolgreich
				content = null;
			}

			//Prüfe, ob der linke Rand entfernt werden kann
			if (before is not null && before.IsEmpty)
			{
				//Entferne die linke Randkomponente
				owner.components.Remove(before);
				removedAnything = true;
			}

			//Prüfe, ob der rechte Rand entfernt werden kann
			if (after is not null && after != before && after.IsEmpty)
			{
				//Entferne die rechte Randkomponente
				owner.components.Remove(after);
				removedAnything = true;
			}

			//Prüfe, ob Komponenten zusammengefügt werden können. Beginne mit der Komponente links vom linken Rand
			var stopCombining = false;
			for (var current = Math.Max(beforeIndex - 1, 0); current < owner.components.Count - 1 && !stopCombining; current++)
			{
				//Kann die Komponente mit der darauf folgenden zusammengeführt werden?
				var component = owner.components[current];
				var nextComponent = owner.components[current + 1];
				if (component.TryMerge(nextComponent, int.MaxValue, formatter))
				{
					//Entferne die folgende Komponente
					owner.components.RemoveAt(current + 1);
					removedAnything = true;
					current--;
				}

				//Höre nach dem rechten Rand auf
				if (nextComponent == after)
					stopCombining = true;
			}

			//Nicht erfolgreich?
			if (!removedAnything && addedContentLength == 0)
				return new LineEditResult(false, null);

			//Modified-Event
			owner.RaiseModified(new ModifiedEventArgs(owner));
			return new LineEditResult(true, new SimpleRange(selectionRange.Start + addedContentLength, selectionRange.Start + addedContentLength));
		}

		private List<VarietyComponent> CreateComponentsForContent(string content)
		{
			if (string.IsNullOrEmpty(content))
				throw new InvalidOperationException("Tried to insert empty content");

			//Trenne Wörter
			var result = new List<VarietyComponent>();
			var index = 0;
			do
			{
				//Finde Länge des aktuellen Typs (Wort oder Leerstelle)
				var isSpace = char.IsWhiteSpace(content[index]);
				var length = content.Skip(index + 1)
					.Select((c, i) => (Char: c, Index: i + 1))
					.SkipWhile(c => char.IsWhiteSpace(c.Char) == isSpace)
					.Select(c => (int?)c.Index)
					.FirstOrDefault()
					?? content.Length - index;

				//Erzeuge ein Wort der entsprechenden Länge
				var word = content.Substring(index, length);
				result.Add(VarietyComponent.FromString(word));
				index += length;

				//Safeguard: length darf nie 0 sein
				if (length == 0)
					throw new InvalidOperationException("Error creating content components");
			}
			while (index < content.Length);

			return result;
		}
	}

	private class AttachmentEditing : ISheetDisplayLineEditing
	{
		private readonly SheetVarietyLine owner;

		public AttachmentEditing(SheetVarietyLine owner)
		{
			this.owner = owner;
		}

		public LineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false)
		{
			//Ist der Bereich leer?
			if (selectionRange.Length == 0)
			{
				if (forward)
					selectionRange = new SimpleRange(selectionRange.Start, selectionRange.Start + 1);
				else
					selectionRange = new SimpleRange(selectionRange.Start - 1, selectionRange.Start);
			}

			return DeleteAndInsertContent(selectionRange, formatter, null);
		}

		public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter)
		{
			return DeleteAndInsertContent(selectionRange, formatter, content);
		}

		private LineEditResult DeleteAndInsertContent(SimpleRange selectionRange, ISheetFormatter? formatter, string? content)
		{
			//Finde alle Attachments im Bereich
			//var attachments = owner.components.OfType<VarietyComponent>()
			//	.SelectMany(c => c.Attachments.Select(a => (Component: c, Attachment: a)))
			//	.Where(a => a.Attachment.Offset >= selectionRange.Start && a.Attachment.Offset + a.Attachment.Length <= selectionRange.End)
			//	.ToList();

			throw new NotImplementedException();
		}
	}
	#endregion

	#region Content
	[Flags]
	public enum AllowedType
	{
		None = 0,
		Chord = 1,
	}

	internal class MaybeChord
	{
		public string? Text { get; private set; }
		public Chord? Chord { get; private set; }

		public AllowedType AllowedTypes { get; }

		public bool IsEmpty => Chord is null && string.IsNullOrEmpty(Text);
		public bool IsSpace => Chord is null && string.IsNullOrWhiteSpace(Text);

		public MaybeChord(string text, AllowedType allowedTypes = AllowedType.Chord)
		{
			Text = text;
			AllowedTypes = allowedTypes;
		}

		public MaybeChord(Chord chord, AllowedType allowedTypes = AllowedType.Chord)
		{
			Chord = chord;
			AllowedTypes = allowedTypes;
		}

		public static MaybeChord FromString(string content, AllowedType allowedTypes = AllowedType.Chord)
		{
			var result = new MaybeChord(content, allowedTypes);
			result.SetContent(content);
			return result;
		}

		public static MaybeChord CreateSpace(int length, AllowedType allowedTypes = AllowedType.Chord)
			=> new MaybeChord(new string(' ', length), allowedTypes);

		public SheetDisplayLineElement CreateElement(ISheetFormatter? formatter)
			=> Chord is not null ? new SheetDisplayLineChord(Chord)
			: string.IsNullOrWhiteSpace(Text) ? new SheetDisplayLineSpace(Text?.Length ?? 0)
			: new SheetDisplayLineText(Text ?? string.Empty);

		public SheetDisplayLineElement CreateElement(int offset, int length, ISheetFormatter? formatter)
		{
			//Trenne Inhalt auf
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			if (offset == 0 && length >= textContent.Length)
				return CreateElement(formatter);

			//Bilde Substring
			var subContent = MaybeChord.FromString(textContent.Substring(offset, Math.Min(length, textContent.Length - offset)));
			return subContent.CreateElement(formatter);
		}

		public int GetLength(ISheetFormatter? formatter)
			=> Text?.Length
			?? Chord?.ToString(formatter).Length
			?? 0;

		internal bool RemoveContent(int offset, int length, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter);
			if (textContent is null) return false;

			//Kürze den Textinhalt
			var newContent = textContent.Remove(offset, Math.Min(length, textContent.Length - offset));
			if (newContent == textContent) return false;

			//Setze den neuen Inhalt
			SetContent(newContent);
			return true;
		}

		internal void AppendContent(string content, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter);

			//Füge den Textinhalt hinzu
			var newContent = textContent + content;
			SetContent(newContent);
		}

		internal void MergeContents(MaybeChord content, int offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;
			var afterTextContent = content.Text ?? content.Chord?.ToString(formatter);

			//Füge den Textinhalt hinzu
			var stringOffset = Math.Min(offset, textContent.Length);
			var newContent = textContent[0..stringOffset] + afterTextContent;
			if (offset < textContent.Length)
				newContent += textContent[stringOffset..];
			SetContent(newContent);
		}

		internal MaybeChord SplitEnd(int offset, ISheetFormatter? formatter)
		{
			//Textinhalt
			var textContent = Text ?? Chord?.ToString(formatter) ?? string.Empty;

			//Trenne den Textinhalt auf
			var newContent = textContent[..offset];
			var newEndContent = textContent[offset..];

			//Setze den neuen Inhalt
			SetContent(newContent);

			//Erzeuge das neue Ende
			return MaybeChord.FromString(newEndContent);
		}

		private void SetContent(string content)
		{
			if ((AllowedTypes & AllowedType.Chord) != 0)
			{
				//Versuche den Inhalt als Akkord zu lesen
				var chordLength = Chord.TryRead(content, out var chord);
				if (chord is not null && chordLength == content.Length)
				{
					//Der Inhalt ist ein Akkord
					Chord = chord;
					Text = null;
					return;
				}
			}

			//Der Inhalt ist kein Akkord
			Text = content;
			Chord = null;
		}

		#region Operators
		public override string? ToString() => Text ?? Chord?.ToString();
		#endregion
	}
	#endregion

	#region Components
	internal readonly record struct RenderBounds(int StartOffset, int EndOffset)
	{
		public int Length => EndOffset - StartOffset;
	}

	public abstract class Component
	{
		public abstract bool IsEmpty { get; }

		internal abstract RenderBounds ContentRenderBounds { get; }
		internal abstract RenderBounds TotalRenderBounds { get; }

		public abstract void BuildLines(LineBuilders builders, ISheetBuilderFormatter? formatter);

		internal abstract bool RemoveContent(int offet, int length, ISheetFormatter? formatter);
		internal abstract bool TryMerge(Component merge, int offset, ISheetFormatter? formatter);
		internal abstract Component SplitEnd(int offset, ISheetFormatter? formatter);

		public record struct LineBuilders(SheetVarietyLine Owner, SheetDisplayTextLine.Builder TextLine, SheetDisplayChordLine.Builder ChordLine)
		{
			public int CurrentLength => Math.Max(TextLine.CurrentLength, ChordLine.CurrentLength);
		}
	}

	public sealed class VarietyComponent : Component
	{
		private readonly List<Attachment> attachments = new();
		public IReadOnlyList<Attachment> Attachments => attachments;

		internal MaybeChord Content { get; }

		public override bool IsEmpty => Content.IsEmpty;

		private RenderBounds contentRenderBounds;
		internal override RenderBounds ContentRenderBounds => contentRenderBounds;

		private RenderBounds totalRenderBounds;
		internal override RenderBounds TotalRenderBounds => totalRenderBounds;

		private VarietyComponent(MaybeChord content)
		{
			Content = content;
		}

		public VarietyComponent(string text, AllowedType allowedTypes = AllowedType.None)
		{
			Content = new(text, allowedTypes);
		}

		public VarietyComponent(Chord chord, AllowedType allowedTypes = AllowedType.Chord)
		{
			Content = new(chord, allowedTypes);
		}

		public static VarietyComponent FromString(string content, AllowedType allowedTypes = AllowedType.Chord)
			=> new VarietyComponent(MaybeChord.FromString(content, allowedTypes));

		public static VarietyComponent CreateSpace(int length, AllowedType allowedTypes = AllowedType.None)
			=> new VarietyComponent(MaybeChord.CreateSpace(length, allowedTypes));

		#region Display
		public override void BuildLines(LineBuilders builders, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Finde das erste Attachment
			var firstAttachment = attachments.FirstOrDefault(a => a.CreateDisplayAttachment(out _, formatter) is not null);
			if (firstAttachment is not null)
			{
				//An welchen Offset soll das Attachment geschrieben werden?
				var targetOffset = builders.ChordLine.CurrentLength + 1;

				//Wie viel mehr Platz wird auf der Akkordzeile benötigt, damit das Attachment passt?
				var difference = targetOffset - builders.TextLine.CurrentLength - firstAttachment.Offset;

				//Verlängere die Textzeile um diese Differenz
				builders.TextLine.ExtendLength(0, difference);
			}

			//Speichere aktuelle Textlänge für Render Bounds
			var textStartIndex = builders.TextLine.CurrentLength;
			var chordStartIndex = builders.ChordLine.CurrentLength;

			//Trenne den Text an Attachments
			Attachment? currentAttachment = null;
			foreach (var nextAttachment in attachments.Prepend(new EmptyAttachmentStub(0)).Append(new EmptyAttachmentStub(contentLength)))
			{
				//Merke das erste Attachment
				if (currentAttachment is null)
				{
					currentAttachment = nextAttachment;
					continue;
				}

				//Berechne Textlänge
				var textLength = nextAttachment.Offset - currentAttachment.Offset;
				if (textLength > 0)
				{
					//Wird ein Attachment geschrieben?
					var displayAttachment = currentAttachment.CreateDisplayAttachment(out var setAttachmentRenderBounds, formatter);
					if (displayAttachment is not null)
					{
						//Stelle sicher, dass die Textzeile bisher so lang wie die Akkordzeile ist, um Content und Attachment zusammenzuhalten
						var textLineGap = builders.ChordLine.CurrentLength - builders.TextLine.CurrentLength;
						if (builders.TextLine.CurrentLength >= 0) textLineGap++;
						if (textLineGap > 0)
						{
							builders.TextLine.Append(new SheetDisplayLineHyphen(textLineGap), formatter);
							builders.TextLine.ExtendLength(builders.ChordLine.CurrentLength, 0);
						}
					}

					//Schreibe den Text
					var contentElement = Content.CreateElement(currentAttachment.Offset, textLength, formatter);
					var lengthBefore = builders.TextLine.CurrentLength;
					builders.TextLine.Append(contentElement, formatter);
					var contentBounds = new RenderBounds(lengthBefore, builders.TextLine.CurrentLength);
					contentRenderBounds = contentBounds;

					//Schreibe ggf. das Attachment
					if (displayAttachment is not null)
					{
						//Stelle sicher, dass die Akkordzeile bisher so lang wie die Textzeile ist, um Content und Attachment zusammenzuhalten
						builders.ChordLine.ExtendLength(lengthBefore, 0);

						//Schreibe das Attachment
						lengthBefore = builders.ChordLine.CurrentLength;
						builders.ChordLine.Append(displayAttachment, formatter);
						var attachmentBounds = new RenderBounds(lengthBefore, builders.ChordLine.CurrentLength);
						setAttachmentRenderBounds(attachmentBounds);
					}
				}

				//Merke das Attachment
				currentAttachment = nextAttachment;
			}

			//Berechne Render Bounds
			contentRenderBounds = new(textStartIndex, builders.TextLine.CurrentLength);
			totalRenderBounds = firstAttachment is null ? contentRenderBounds
				: new(textStartIndex, builders.CurrentLength);
		}

		//private List<DisplayBlock> GetDisplay(ISheetFormatter? formatter)
		//{
		//	if (displayBlocksCache is not null) return displayBlocksCache;

		//	//Erzeuge neuen Cache
		//	var cache = new List<DisplayBlock>();

		//	//Trenne den Text an Attachments
		//	if (attachments.Count == 0)
		//	{
		//		cache.Add(new DisplayBlock(Content.CreateElement(formatter)));
		//	}
		//	else if (attachments.Count == 1)
		//	{
		//		if (attachments[0].Offset == 0)
		//		{
		//			cache.Add(new DisplayBlock(Content.CreateElement(formatter),
		//				attachments[0].CreateDisplayAttachment(formatter)));
		//		}
		//		else
		//		{
		//			cache.Add(new DisplayBlock(Content.CreateElement(0, attachments[0].Offset, formatter)));
		//			cache.Add(new DisplayBlock(Content.CreateElement(attachments[0].Offset, int.MaxValue, formatter),
		//				attachments[^1].CreateDisplayAttachment(formatter)));
		//		}
		//	}
		//	else if (attachments[0].Offset == 0)
		//	{
		//		for (var i = 1; i < attachments.Count; i++)
		//			cache.Add(new DisplayBlock(Content.CreateElement(attachments[i - 1].Offset, attachments[i].Offset - attachments[i - 1].Offset, formatter),
		//				attachments[i - 1].CreateDisplayAttachment(formatter)));

		//		cache.Add(new DisplayBlock(Content.CreateElement(attachments[^1].Offset, int.MaxValue, formatter),
		//			attachments[^1].CreateDisplayAttachment(formatter)));
		//	}
		//	else
		//	{
		//		cache.Add(new DisplayBlock(Content.CreateElement(0, attachments[0].Offset, formatter)));
		//		for (var i = 1; i < attachments.Count; i++)
		//			cache.Add(new DisplayBlock(Content.CreateElement(attachments[i - 1].Offset, attachments[i].Offset - attachments[i - 1].Offset, formatter),
		//				attachments[i - 1].CreateDisplayAttachment(formatter)));
		//		cache.Add(new DisplayBlock(Content.CreateElement(attachments[^1].Offset, int.MaxValue, formatter),
		//			attachments[^1].CreateDisplayAttachment(formatter)));
		//	}

		//	//Speichere den Cache
		//	return displayBlocksCache = cache;
		//}

		private void ResetDisplayCache()
		{
			//=> displayBlocksCache = null;
		}

		internal record DisplayBlock
		{
			public SheetDisplayLineElement Content { get; }
			public SheetDisplayLineElement? Attachment { get; }

			public DisplayBlock(SheetDisplayLineElement content, SheetDisplayLineElement? attachment = null)
			{
				if (content is SheetDisplayLineText text && attachment is not null)
				{
					content = new SheetDisplayLineAnchorText(text.Text)
					{
						Targets = [attachment]
					};
				}

				Content = content;
				Attachment = attachment;
			}
		}

		private sealed class EmptyAttachmentStub : Attachment
		{
			internal override RenderBounds RenderBounds { get; private protected set; }

			public EmptyAttachmentStub(int offset)
				: base(offset)
			{
				RenderBounds = new(Offset, Offset);
			}

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return null;
			}

			private static void SetRenderBounds(RenderBounds _) { }
		}
		#endregion

		#region Editing
		internal override bool RemoveContent(int offset, int length, ISheetFormatter? formatter)
		{
			//Speichere die Länge vor der Bearbeitung
			var lengthBefore = Content.GetLength(formatter);
			if (length == 0 || offset >= lengthBefore) return false;

			//Entferne den Inhalt
			if (!Content.RemoveContent(offset, length, formatter))
				return false;

			//Berechne die Länge nach der Bearbeitung
			ResetDisplayCache();
			var lengthAfter = Content.GetLength(formatter);

			//Hat sich die Länge des Inhalts unerwartet verändert?
			var moved = lengthBefore - lengthAfter - length;

			//Entferne alle Attachments, die im Bereich liegen und verschiebe die Attachments, die dahinter liegen.
			//Behalte Attachments, die auf Offset 0 liegen
			var endOffset = offset + length;
			attachments.RemoveAll(a =>
			{
				//Liegt das Attachment vor dem Bereich?
				if (a.Offset < offset) return false;

				//Liegt das Attachment im Bereich?
				if (a.Offset < endOffset) return a.Offset != 0;

				//Verschiebe Attachments nach dem Bereich
				if (moved != 0)
				a.SetOffset(a.Offset + moved);
				return false;
			});
			ResetDisplayCache();
			return true;
		}

		internal override bool TryMerge(Component merge, int offset, ISheetFormatter? formatter)
		{
			//Ist das nachfolgende Element kein VarietyComponent?
			if (merge is not VarietyComponent varietyMerge)
				return false;

			//Besteht eine, aber nicht beide Komponenten nur aus Leerzeichen?
			if (Content.IsSpace != varietyMerge.Content.IsSpace)
				return false;

			//Füge Inhalt zusammen
			var lengthBefore = Content.GetLength(formatter);
			var mergeLengthBefore = varietyMerge.Content.GetLength(formatter);
			Content.MergeContents(varietyMerge.Content, offset, formatter);
			ResetDisplayCache();

			//Verschiebe alle Attachments nach dem Offset
			var lengthNow = Content.GetLength(formatter);
			var moved = lengthNow - lengthBefore - mergeLengthBefore;
			if (moved != 0)
				foreach (var attachment in attachments)
					if (attachment.Offset >= offset)
						attachment.SetOffset(attachment.Offset + moved);

			//Füge alle Attachments zusammen und verschiebe dabei alle Attachments des eingefügten Elements
			var newAttachmentsMove = lengthBefore + moved;
			attachments.AddRange(varietyMerge.Attachments.Select(a =>
			{
				//Verschiebe das Attachment
				a.SetOffset(a.Offset + newAttachmentsMove);
				return a;
			}));

			//Zusammenführung erfolgreich
			ResetDisplayCache();
			return true;
		}

		internal override Component SplitEnd(int offset, ISheetFormatter? formatter)
		{
			//Trenne den Inhalt
			var newEndContent = Content.SplitEnd(offset, formatter);
			var newEnd = new VarietyComponent(newEndContent);

			//Entferne alle Attachments nach der Trennung und füge sie dem neuen Ende hinzu
			attachments.RemoveAll(a =>
			{
				if (a.Offset < offset) return false;

				//Verschiebe das Attachment
				a.SetOffset(a.Offset - offset);
				newEnd.attachments.Add(a);
				return true;
			});

			ResetDisplayCache();
			return newEnd;
		}

		public void AddAttachment(Attachment attachment)
		{
			attachments.Add(attachment);
			ResetDisplayCache();
		}

		public void AddAttachments(IEnumerable<Attachment> attachments)
		{
			this.attachments.AddRange(attachments);
			this.attachments.Sort((a1, a2) => a1.Offset - a2.Offset);
			ResetDisplayCache();
		}
		#endregion

		#region Operators
		public override string? ToString() => Content.ToString();
		#endregion

		public abstract class Attachment
		{
			public int Offset { get; protected set; }
			internal abstract RenderBounds RenderBounds { get; private protected set; }

			protected Attachment(int offset)
			{
				if (offset < 0)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal void SetOffset(int offset)
			{
				if (offset < 0)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal abstract SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter);
		}

		public sealed class VarietyAttachment : Attachment
		{
			internal MaybeChord Content { get; }

			internal override RenderBounds RenderBounds { get; private protected set; }

			public VarietyAttachment(int offset, string text)
				: base(offset)
			{
				Content = new(text);
			}

			public VarietyAttachment(int offset, Chord chord)
				: base(offset)
			{
				Content = new(chord);
			}

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return Content.CreateElement(formatter);
			}

			private void SetRenderBounds(RenderBounds bounds)
				=> RenderBounds = bounds;
		}
	}
	#endregion
}
