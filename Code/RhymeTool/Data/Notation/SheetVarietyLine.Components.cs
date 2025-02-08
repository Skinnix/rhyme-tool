using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Editing;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

partial class SheetVarietyLine
{
	internal record DisplayRenderBounds(int StartOffset, int EndOffset, IReadOnlyList<SheetDisplayLineElement> DisplayElements)
	{
		public static readonly DisplayRenderBounds Empty = new(0, 0, []);

		public int Length => EndOffset - StartOffset;

		public VirtualContentOffset GetContentOffset(int displayOffset)
		{
			//Finde das Element, in dem der Offset liegt
			var display = DisplayElements.LastOrDefault(d => d.Slice.HasValue && d.DisplayOffset <= displayOffset);
			if (display is null)
				return new(ContentOffset.Zero, displayOffset);

			//Ist das Element virtuell?
			var offsetFromStart = displayOffset - display.DisplayOffset;
			if (display.Slice!.Value.IsVirtual)
				return new(display.Slice.Value.ContentOffset, offsetFromStart);

			//Berechne den tatsächlichen ContentOffset
			var contentOffset = display.Slice.Value.ContentOffset + new ContentOffset(offsetFromStart);
			return new(contentOffset, 0);
		}

		public int GetDisplayOffset(ContentOffset contentOffset, bool keepRight = false)
		{
			if (!keepRight)
			{
				//Finde das letzte nicht-virtuelle Element, das vor dem Offset liegt
				var display = DisplayElements.LastOrDefault(d => !d.Slice!.Value.IsVirtual && d.Slice!.Value.ContentOffset <= contentOffset);

				//Fallback, falls kein Element gefunden wurde
				if (display is null)
				{
					if (contentOffset == ContentOffset.Zero)
						return StartOffset;
					else
						return EndOffset;
				}

				//Berechne den Offset
				return display.DisplayOffset + contentOffset.Value - display.Slice!.Value.ContentOffset.Value;
			}
			else
			{
				//Finde das letzte Element, das vor dem Offset liegt
				var display = DisplayElements
					.Select((d, i) => (Display: d, Index: i))
					.LastOrDefault(d => d.Display.Slice!.Value.ContentOffset <= contentOffset);

				//Fallback, falls kein Element gefunden wurde
				if (display.Display is null)
				{
					if (contentOffset == ContentOffset.Zero)
						return StartOffset;
					else
						return EndOffset;
				}

				//Ist das Element virtuell und entspricht genau dem Offset?
				if (display.Display.Slice!.Value.IsVirtual && display.Display.Slice!.Value.ContentOffset == contentOffset)
				{
					//Gehe zum Anfang des nächsten Elements
					if (display.Index + 1 < DisplayElements.Count)
						return DisplayElements[display.Index + 1].DisplayOffset;
					else
						return EndOffset;
				}

				//Berechne den Offset
				return display.Display.DisplayOffset + contentOffset.Value - display.Display.Slice!.Value.ContentOffset.Value;
			}
		}
	}

	public abstract class Component : SheetLineComponent
	{
		internal SheetVarietyLine? Owner { get; set; }

		public abstract bool IsEmpty { get; }

		internal abstract DisplayRenderBounds DisplayRenderBounds { get; }
		internal abstract RenderBounds TotalRenderBounds { get; }
		internal abstract IReadOnlyList<SheetDisplayLineElement> ContentElements { get; }

		internal abstract void BuildLines(LineBuilders builders, int componentIndex, ISheetBuilderFormatter? formatter);

		internal abstract bool TryRemoveContent(ContentOffset offet, ContentOffset length, ISheetEditorFormatter? formatter);
		internal abstract bool TryReplaceContent(ComponentContent newContent, ISheetEditorFormatter? formatter);
		internal abstract MergeResult? TryMerge(Component next, ContentOffset offset, ISheetEditorFormatter? formatter);
		internal abstract Component SplitEnd(ContentOffset offset, ISheetEditorFormatter? formatter);

		public abstract Stored Store();

		internal class LineBuilders
		{
			private int breakPointIndex;

			public SheetVarietyLine Owner { get; }
			public SheetDisplayTextLine.Builder TextLine { get; }
			public SheetDisplayChordLine.Builder ChordLine { get; }

			public IEnumerable<SheetDisplayLineBuilderBase> AllLines => [TextLine, ChordLine];

			public bool IsTitleLine { get; init; }
			public bool IsRenderingTitle { get; set; }

			public int CurrentLength => Math.Max(TextLine.CurrentLength, ChordLine.CurrentLength);

			public LineBuilders(SheetVarietyLine owner, SheetDisplayTextLine.Builder textLine, SheetDisplayChordLine.Builder chordLine)
			{
				Owner = owner;
				TextLine = textLine;
				ChordLine = chordLine;
			}

			public void AddBreakPoint(SheetLineComponent component, int textLineOffset, int chordLineOffset, ISheetFormatter? formatter = null)
			{
				var index = breakPointIndex++;

				TextLine.Append(new SheetDisplayLineBreakPoint(index, textLineOffset)
				{
					Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
				});
				ChordLine.Append(new SheetDisplayLineBreakPoint(index, chordLineOffset)
				{
					Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
				});
			}

			public (SheetDisplayLineBreakPoint Text, SheetDisplayLineBreakPoint Attachment) CreateBreakPoints(SheetLineComponent component, int textLineOffset, int chordLineOffset)
			{
				var index = breakPointIndex++;

				return (
					new SheetDisplayLineBreakPoint(index, textLineOffset)
					{
						Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
					},
					new SheetDisplayLineBreakPoint(index, chordLineOffset)
					{
						Slice = new SheetDisplaySliceInfo(component, ContentOffset.Zero, IsVirtual: true)
					});
			}
		}

		internal class Collection : IList<Component>
		{
			private readonly SheetVarietyLine owner;
			private readonly List<Component> components;

			public int Count => components.Count;
			public bool IsReadOnly => false;

			public Component this[int index]
			{
				get => components[index];
				set => components[index] = value;
			}

			public Collection(SheetVarietyLine owner)
			{
				this.owner = owner;
				this.components = new();
			}

			public Collection(SheetVarietyLine owner, IEnumerable<Component> components)
			{
				this.owner = owner;
				this.components = new(components.Select(c =>
				{
					c.Owner = owner;
					return c;
				}));
			}

			public bool Contains(Component item) => components.Contains(item);
			public int IndexOf(Component item) => components.IndexOf(item);
			public void CopyTo(Component[] array, int arrayIndex) => components.CopyTo(array, arrayIndex);

			public void Add(Component item)
			{
				components.Add(item);
				item.Owner = owner;
			}

			public void AddRange(IEnumerable<Component> components)
				=> this.components.AddRange(components.Select(c =>
				{
					c.Owner = owner;
					return c;
				}));

			public void InsertRange(int index, IEnumerable<Component> components)
				=> this.components.InsertRange(index, components.Select(c =>
				{
					c.Owner = owner;
					return c;
				}));

			public void Insert(int index, Component item)
			{
				components.Insert(index, item);
				item.Owner = owner;
			}

			public bool Remove(Component item)
			{
				if (!components.Remove(item))
					return false;

				if (item.Owner == owner)
					item.Owner = null;

				return true;
			}

			public void RemoveAt(int index)
			{
				var item = components[index];
				components.RemoveAt(index);

				if (item.Owner == owner)
					item.Owner = null;
			}

			public void RemoveRange(int index, int count)
			{
				var end = index + count;
				for (var i = index; i < end; i++)
				{
					var component = components[i];
					if (component.Owner == owner)
						component.Owner = null;
				}

				components.RemoveRange(index, count);
			}

			public void RemoveAll(Func<Component, bool> match)
				=> components.RemoveAll(c =>
				{
					if (!match(c))
						return false;

					if (c.Owner == owner)
						c.Owner = null;

					return true;
				});

			public void Clear()
			{
				foreach (var component in components)
					if (component.Owner == owner)
						component.Owner = null;

				components.Clear();
			}

			public IEnumerator<Component> GetEnumerator() => components.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public Stored Store() => new(this);

			public readonly struct Stored : ISelfStored<Stored, Collection, SheetVarietyLine>
			{
				private readonly Component.Stored[]? components;

				internal Stored(Collection collection)
				{
					if (collection.Count == 0)
					{
						components = null;
					}
					else
					{
						components = new Component.Stored[collection.Count];
						foreach ((var i, var component) in collection.Index())
							components[i] = component.Store();

						components = ArrayCache.Cache(components);
					}
				}

				private Stored(Component.Stored[] components)
				{
					this.components = components;
				}

				public Collection Restore(SheetVarietyLine owner)
					=> new Collection(owner, components?.Select(c => c.Restore()) ?? []);

				/*public Stored OptimizeWith(Stored line, out bool isEqual)
				{
					isEqual = line.components.Length == components.Length;
					var newComponents = new Component.Stored[components.Length];
					for (var i = 0; i < components.Length; i++)
					{
						if (i >= line.components.Length)
						{
							isEqual = false;
							newComponents[i] = components[i];
						}
						else
						{
							newComponents[i] = components[i].OptimizeWith(line.components[i], out var componentEqual);
							if (!componentEqual)
							{
								isEqual = false;
								newComponents[i] = components[i].OptimizeWith(line.components);
							}
						}
					}

					if (isEqual)
						return line;

					return new(newComponents);
				}*/
			}
		}

		public readonly struct Stored : IStored<Component>
		{
			private readonly ComponentContent content;
			private readonly VarietyComponent.Attachment.Stored[]? attachments;

			internal Stored(ComponentContent content, IReadOnlyCollection<VarietyComponent.Attachment> attachments)
			{
				this.content = new(ReferenceCache.Cache(content.Content));

				if (attachments.Count == 0)
				{
					this.attachments = null;
				}
				else
				{
					this.attachments = new VarietyComponent.Attachment.Stored[attachments.Count];
					foreach ((var i, var attachment) in attachments.Index())
						this.attachments[i] = attachment.Store();

					this.attachments = ArrayCache.Cache(this.attachments);
				}
			}

			private Stored(ComponentContent content, VarietyComponent.Attachment.Stored[] attachments)
			{
				this.content = content;
				this.attachments = attachments;
			}

			public Component Restore()
			{
				var component = new VarietyComponent(content);
				component.AddAttachments(attachments?.Select(a => a.Restore()) ?? []);
				return component;
			}

			/*public Stored OptimizeWith(Stored component, out bool isEqual)
			{
				isEqual = attachments.Length == component.attachments.Length
					&& attachments.SequenceEqual(component.attachments);

				if (!isEqual)
					return this;

				if (content.Equals(component.content))
					return component;

				isEqual = false;
				return new(content, component.attachments);
			}

			public Stored OptimizeWith(IReadOnlyCollection<Stored> attachments)
			{
				Stored? attachmentsCandidate = null;
				foreach (var attachment in attachments)
				{
					if (this.attachments.Length != attachment.attachments.Length)
						continue;

					if (!this.attachments.SequenceEqual(attachment.attachments))
						continue;

					attachmentsCandidate = attachment;
					if (!content.Equals(attachment.content))
						continue;

					return attachment;
				}

				if (attachmentsCandidate is not null)
					return new(content, attachmentsCandidate.Value.attachments);

				return this;
			}*/
		}
	}

	public sealed class VarietyComponent : Component
	{
		private readonly List<Attachment> attachments = new();
		public IReadOnlyList<Attachment> Attachments => attachments;

		internal ComponentContent Content { get; private set; }

		public override bool IsEmpty => Content.IsEmpty;

		private DisplayRenderBounds displayRenderBounds = DisplayRenderBounds.Empty;
		internal override DisplayRenderBounds DisplayRenderBounds => displayRenderBounds;

		private RenderBounds totalRenderBounds = RenderBounds.Empty;
		internal override RenderBounds TotalRenderBounds => totalRenderBounds;

		private List<SheetDisplayLineElement> contentElements = new();
		internal override IReadOnlyList<SheetDisplayLineElement> ContentElements => contentElements;

		internal VarietyComponent(ComponentContent content)
		{
			Content = content;
		}

		public VarietyComponent(string text)
		{
			Content = new(text);
		}

		public VarietyComponent(Chord chord)
		{
			Content = new(chord);
		}

		public static VarietyComponent FromString(string content, ISheetEditorFormatter? formatter, SpecialContentType allowedTypes = SpecialContentType.All)
			=> new VarietyComponent(ComponentContent.FromString(content, formatter, allowedTypes));

		public static VarietyComponent CreateSpace(ContentOffset length, ISheetEditorFormatter? formatter)
			=> new VarietyComponent(ComponentContent.CreateSpace(length, formatter));

		public override Stored Store() => new(Content, attachments);

		#region Display
		internal override void BuildLines(LineBuilders builders, int componentIndex, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Verlängere die Akkordzeile auf die Länge der Textzeile (macht das Spacing einfacher und schließt keinen Fall aus)
			builders.ChordLine.ExtendLength(builders.TextLine.CurrentLength, 0);

			//Finde das erste Attachment
			(Attachment Attachment, SheetDisplayLineElement Display) firstAttachment = attachments
				.Select((a, i) => (Attachment: a, Display: a.CreateDisplayAttachment(out _, formatter)))
				.FirstOrDefault(a => a.Display is not null)!;
			(SheetDisplayLineBreakPoint Text, SheetDisplayLineBreakPoint Attachment)? firstAttachmentBreakpoint = null;
			int firstAttachmentOffset = 0;
			if (firstAttachment.Attachment is null)
			{
				//Die Komponente hat keine Attachments

				//Ist die Komponente nur ein einzelnes Satzzeichen und war die vorherige Komponente ein Wort?
				if (Content.Type == ContentType.Punctuation && builders.TextLine.CurrentLength == builders.TextLine.CurrentNonSpaceLength)
				{
					//Schreibe nur das Satzzeichen
					var textStartLength = builders.TextLine.CurrentLength;
					var displayText = new SheetDisplayLineText((string)Content.Content.Value!)
					{
						Slice = new SheetDisplaySliceInfo(this, ContentOffset.Zero)
					};
					builders.TextLine.Append(displayText);

					//Berechne Render Bounds
					contentElements = [displayText];
					displayRenderBounds = new(textStartLength, builders.TextLine.CurrentLength, contentElements);
					totalRenderBounds = new(displayRenderBounds.StartOffset, displayRenderBounds.EndOffset);
					return;
				}

				//Füge direkt einen Breakpoint ein
				builders.AddBreakPoint(this, 0, 0, formatter);
			}
			else
			{
				//An welchen Offset soll das Attachment geschrieben werden?
				var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, firstAttachment.Display)
					?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
				firstAttachmentOffset = builders.ChordLine.CurrentNonSpaceLength + spaceBefore;

				//Wie viel Platz wird auf der Akkordzeile benötigt, damit das Attachment passt?
				var required = firstAttachmentOffset - firstAttachment.Attachment.Offset.Value;
				builders.ChordLine.ExtendLength(required, 0, formatter);

				//Verlängere die Textzeile auch um diese Differenz
				builders.TextLine.ExtendLength(required, 0, formatter);

				//Füge einen Breakpoint auf der Textzeile ein
				firstAttachmentBreakpoint = builders.CreateBreakPoints(this, 0, firstAttachment.Attachment.Offset.Value);
				builders.TextLine.Append(firstAttachmentBreakpoint.Value.Text, formatter);
			}

			//Speichere aktuelle Textlänge für Render Bounds
			var textStartIndex = builders.TextLine.CurrentLength;
			var chordStartIndex = builders.ChordLine.CurrentLength;

			//Trenne den Text an Attachments
			var contentDisplayElements = new List<SheetDisplayLineElement>();
			var isTitleLine = Owner?.IsTitleLine(out _) == true;
			foreach (var block in GetDisplayColumns(componentIndex, formatter))
			{
				//Wird ein Attachment geschrieben?
				if (block.Attachment is not null)
				{
					//Lasse Platz vor dem Attachment
					var spaceBefore = formatter?.SpaceBefore(builders.Owner, builders.ChordLine, block.Attachment)
						?? (builders.ChordLine.CurrentLength == 0 ? 0 : 1);
					builders.ChordLine.EnsureSpaceBefore(spaceBefore, formatter);

					//Stelle sicher, dass die Textzeile bisher so lang wie die Akkordzeile ist, um Content und Attachment zusammenzuhalten
					var textLineGap = builders.ChordLine.CurrentLength - builders.TextLine.CurrentLength;

					//Berechne die Lücke
					if (textLineGap > 0)
					{
						//Schreibe ggf. einen Bindestrich
						var offsetBefore = builders.TextLine.CurrentLength;
						var hyphen = new SheetDisplayLineHyphen(textLineGap)
						{
							Slice = block.Content.Slice!.Value with
							{
								IsVirtual = true,
							}
						};
						contentDisplayElements.Add(hyphen);
						builders.TextLine.Append(hyphen, formatter);
					}
				}

				//Merke die Länge der Textzeile
				var textLineLengthBefore = builders.TextLine.CurrentLength;

				//Ist die Zeile eine Titelzeile?
				var isTitle = false;
				if (isTitleLine)
				{
					foreach (var titleElement in TrySplitTitleElement(block.Content, builders.IsRenderingTitle))
					{
						isTitle = true;

						//An Klammern beginnt/endet der Titel
						if (titleElement is SheetDisplayLineSegmentTitleBracket titleBracket)
							builders.IsRenderingTitle = !builders.IsRenderingTitle;

						//Schreibe Titelelemente
						builders.TextLine.Append(titleElement, formatter);
						contentDisplayElements.Add(titleElement);
					}
				}

				//Kein Titelelement?
				if (!isTitle)
				{
					//Schreibe Inhalt
					builders.TextLine.Append(block.Content, formatter);
					contentDisplayElements.Add(block.Content);
				}

				//Schreibe ggf. das Attachment
				if (block.Attachment is not null)
				{
					//Stelle sicher, dass die Akkordzeile bisher so lang wie die Textzeile ist, um Content und Attachment zusammenzuhalten
					builders.ChordLine.ExtendLength(textLineLengthBefore, 0);

					//Füge beim ersten Attachment den Breakpoint ein
					if (firstAttachmentBreakpoint is not null)
					{
						builders.ChordLine.Append(firstAttachmentBreakpoint.Value.Attachment with
						{
							StartingPointOffset = builders.ChordLine.CurrentLength - firstAttachmentBreakpoint.Value.Text.DisplayOffset,
						}, formatter);
						firstAttachmentBreakpoint = null;
					}

					//Schreibe das Attachment
					textLineLengthBefore = builders.ChordLine.CurrentLength;
					builders.ChordLine.Append(block.Attachment, formatter);
					var attachmentBounds = new RenderBounds(textLineLengthBefore, builders.ChordLine.CurrentLength);
					block.SetAttachmentRenderBounds?.Invoke(attachmentBounds);
				}
			}

			//Berechne Render Bounds
			displayRenderBounds = new(textStartIndex, builders.TextLine.CurrentLength, contentDisplayElements);
			totalRenderBounds = firstAttachment.Attachment is null ? new(displayRenderBounds.StartOffset, displayRenderBounds.EndOffset)
				: new(textStartIndex, builders.CurrentLength);
			contentElements = contentDisplayElements;
		}

		private IEnumerable<(SheetDisplayLineElement Content, SheetDisplayLineElement? Attachment, Action<RenderBounds>? SetAttachmentRenderBounds)>
			GetDisplayColumns(int componentIndex, ISheetBuilderFormatter? formatter)
		{
			//Berechne Textlänge
			var contentLength = Content.GetLength(formatter);

			//Trenne den Text an Attachments
			Attachment? currentAttachment = null;
			var nextAttachmentIndex = -2;
			var currentAttachmentIndex = 0;
			foreach (var nextAttachment in attachments.Prepend(new EmptyAttachmentStub(ContentOffset.Zero)).Append(new EmptyAttachmentStub(contentLength)))
			{
				nextAttachmentIndex++;

				//Merke das erste Attachment
				if (currentAttachment is null)
				{
					currentAttachment = nextAttachment;
					currentAttachmentIndex = nextAttachmentIndex;
					continue;
				}

				//Berechne Textlänge
				var textLength = nextAttachment.Offset - currentAttachment.Offset;
				if (textLength <= ContentOffset.Zero)
				{
					//Nichts schreiben
					currentAttachment = nextAttachment;
					currentAttachmentIndex = nextAttachmentIndex;
					continue;
				}

				//Erzeuge das Attachment
				var currentOffset = currentAttachment.Offset;
				var slice = new SheetDisplaySliceInfo(this, currentOffset, false);
				var displayAttachment = currentAttachment.CreateDisplayAttachment(out var setAttachmentRenderBounds, formatter);

				//Erzeuge den Inhalt
				var displayContent = Content.CreateDisplayElementPart(slice, currentAttachment.Offset, textLength, formatter);
				yield return (displayContent, displayAttachment, setAttachmentRenderBounds);

				//Nächstes Attachment
				currentAttachment = nextAttachment;
				currentAttachmentIndex = nextAttachmentIndex;
			}
		}

		private static IEnumerable<SheetDisplayLineElement> TrySplitTitleElement(SheetDisplayLineElement element, bool isCurrentlyWritingTitle)
		{
			if (element is not SheetDisplayLineText textElement)
				yield break;

			//Ist der Inhalt der Anfang eines Titels?
			var text = textElement.Text;
			var sliceIndex = 0;
			var contentOffset = element.Slice?.ContentOffset ?? ContentOffset.Zero;
			if (!isCurrentlyWritingTitle)
			{
				if (!text.StartsWith(TITLE_START_DELIMITER))
					yield break; //nur Text

				//Trenne öffnende Klammer
				yield return new SheetDisplayLineSegmentTitleBracket(TITLE_START_DELIMITER.ToString(), true)
				{
					Slice = textElement.Slice,
				};
				if (text.Length == 1)
					yield break;

				sliceIndex++;
				contentOffset += ContentOffset.One;
				text = text[1..];
				isCurrentlyWritingTitle = true;
			}

			//Ist die Komponente auch das Ende des Titels?
			if (text.EndsWith(TITLE_END_DELIMITER))
			{
				//Trenne den Text
				var titleText = text[..^1];
				yield return new SheetDisplayLineSegmentTitleText(titleText)
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = contentOffset,
					},
				};
				contentOffset += new ContentOffset(titleText.Length);

				//Trenne schließende Klammer
				yield return new SheetDisplayLineSegmentTitleBracket(TITLE_END_DELIMITER.ToString(), false)
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = contentOffset,
					},
				};
			}
			else if (isCurrentlyWritingTitle)
			{
				//Trenne den Text
				var titleText = text;
				yield return new SheetDisplayLineSegmentTitleText(titleText)
				{
					Slice = textElement.Slice!.Value with
					{
						ContentOffset = contentOffset,
					},
				};
				contentOffset += new ContentOffset(titleText.Length);
			}
			else
			{
				//Nur Text
				yield break;
			}
		}

		private void ResetDisplayCache()
		{
			//=> displayBlocksCache = null;
		}

		private sealed class EmptyAttachmentStub : Attachment
		{
			internal override RenderBounds RenderBounds
			{
				get => throw new NotSupportedException();
				private protected set => throw new NotSupportedException();
			}

			public override bool IsEmpty => true;

			public EmptyAttachmentStub(ContentOffset offset)
				: base(offset)
			{ }

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return null;
			}

			private static void SetRenderBounds(RenderBounds _) { }

			public override Stored Store() => throw new NotSupportedException();
		}
		#endregion

		#region Editing
		internal override bool TryReplaceContent(ComponentContent newContent, ISheetEditorFormatter? formatter)
		{
			//Passt der Inhaltstyp nicht?
			if (newContent.Type != Content.Type)
				return false;

			if (newContent.Content.Switch(
				_ => true,
				_ => Owner?.GetAllowedTypes(this).HasFlag(SpecialContentType.Chord),
				_ => Owner?.GetAllowedTypes(this).HasFlag(SpecialContentType.Fingering),
				_ => Owner?.GetAllowedTypes(this).HasFlag(SpecialContentType.Rhythm)) != true)
				return false;

			//Ersetze den Inhalt
			Content = newContent;

			//Entferne alle Attachments, die außerhalb des Inhalts liegen
			var lengthAfter = Content.GetLength(formatter);
			attachments.RemoveAll(a => a.Offset >= lengthAfter);
			return true;
		}

		internal override bool TryRemoveContent(ContentOffset offset, ContentOffset length, ISheetEditorFormatter? formatter)
		{
			//Speichere die Länge vor der Bearbeitung
			var lengthBefore = Content.GetLength(formatter);
			if (length == ContentOffset.Zero || offset >= lengthBefore) return false;

			//Entferne den Inhalt
			var removedContent = Content.RemoveContent(offset, length, Owner?.GetAllowedTypes(this) ?? SpecialContentType.None, formatter);
			if (removedContent is null)
				return false;
			Content = removedContent.Value;

			//Berechne die Länge nach der Bearbeitung
			ResetDisplayCache();
			var lengthAfter = Content.GetLength(formatter);

			//Wie hat sich die Länge des Inhalts verändert?
			var moved = lengthAfter - lengthBefore;

			//Entferne alle Attachments, die im Bereich liegen und verschiebe die Attachments, die dahinter liegen.
			//Behalte Attachments, die auf Offset 0 liegen
			var endOffset = offset + length;
			var index = -1;
			attachments.RemoveAll(a =>
			{
				index++;

				//Liegt das Attachment vor dem Bereich?
				if (a.Offset < offset) return false;

				//Liegt das Attachment im Bereich?
				if (a.Offset < endOffset)
				{
					//Würde sich das Attachment mit einem anderen überschneiden?
					if (moved < ContentOffset.Zero && attachments.Skip(index + 1).Any(n => n.Offset + moved <= offset))
						return true;
					else
					{
						//Setze das Attachment an den Anfang des Bereichs
						a.SetOffset(offset);
						return false;
					}
				}

				//Verschiebe Attachments nach dem Bereich
				if (moved != ContentOffset.Zero)
					a.SetOffset(a.Offset + moved);
				return false;
			});
			ResetDisplayCache();
			return true;
		}

		internal override MergeResult? TryMerge(Component next, ContentOffset offset, ISheetEditorFormatter? formatter)
		{
			//Ist das nachfolgende Element kein VarietyComponent?
			if (next is not VarietyComponent varietyMerge)
				return null;

			//Prüfe, zu welchen Typen der Inhalt zusammengeführt werden kann
			var lengthBefore = Content.GetLength(formatter);
			var mergeLengthBefore = varietyMerge.Content.GetLength(formatter);
			var mergeType = CanMergeTo(Content, lengthBefore, varietyMerge.Content, mergeLengthBefore, offset)
				& (Owner?.GetAllowedTypes(this) ?? SpecialContentType.Text);

			//Kann der Inhalt gar nicht zusammengeführt werden?
			if (mergeType == SpecialContentType.None)
				return null;

			//Füge Inhalt zusammen
			var mergeResult = Content.MergeContents(varietyMerge.Content, offset, mergeType, formatter);

			////Passen die Inhaltstypen nicht zusammen?
			//if (!CanAppendTo(Content, varietyMerge.Content, mergeResult.NewContent))
			//	return null;

			Content = mergeResult.NewContent;
			ResetDisplayCache();

			//Verschiebe alle Attachments nach dem Offset
			var lengthNow = Content.GetLength(formatter);
			var moved = lengthNow - lengthBefore;
			if (moved != ContentOffset.Zero)
				foreach (var attachment in attachments)
					if (attachment.Offset >= offset)
						attachment.SetOffset(attachment.Offset + moved);

			//Füge alle Attachments zusammen und verschiebe dabei alle Attachments des eingefügten Elements
			var newAttachmentsMove = lengthNow - mergeLengthBefore;
			attachments.AddRange(varietyMerge.Attachments.Select(a =>
			{
				//Verschiebe das Attachment
				a.SetOffset(a.Offset + newAttachmentsMove);
				return a;
			}));

			//Zusammenführung erfolgreich
			ResetDisplayCache();
			return mergeResult;
		}

		internal override Component SplitEnd(ContentOffset offset, ISheetEditorFormatter? formatter)
		{
			//Trenne den Inhalt
			var allowedTypes = Owner?.GetAllowedTypes(this) ?? SpecialContentType.None;
			var newContents = Content.SplitEnd(offset, allowedTypes, allowedTypes, formatter);
			if (newContents is null)
				throw new ArgumentOutOfRangeException();

			Content = newContents.Value.NewContent;
			var newEnd = new VarietyComponent(newContents.Value.EndContent);

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
			var newIndex = attachments.FindIndex(a => a.Offset > attachment.Offset);
			if (newIndex >= 0)
				attachments.Insert(newIndex, attachment);
			else
				attachments.Add(attachment);

			ResetDisplayCache();
		}

		public void AddAttachments(IEnumerable<Attachment> attachments)
		{
			this.attachments.AddRange(attachments);
			this.attachments.Sort((a1, a2) => a1.Offset.Value - a2.Offset.Value);
			ResetDisplayCache();
		}

		public bool RemoveAttachment(Attachment attachment)
		{
			if (!attachments.Remove(attachment))
				return false;

			ResetDisplayCache();
			return true;
		}

		private static SpecialContentType CanMergeTo(ComponentContent left, ContentOffset leftLength, ComponentContent right, ContentOffset rightLength, ContentOffset offset)
		{
			//Leerzeichen können nur mit Leerzeichen kombiniert werden
			if (left.Type == ContentType.Space)
				return right.Type == ContentType.Space ? SpecialContentType.Text : SpecialContentType.None;

			switch (left.Type)
			{
				//Fingerings nur kombiniert werden, wenn dabei Fingerings oder Text herauskommen
				case ContentType.Fingering:
					return right.Type switch
					{
						ContentType.Punctuation or ContentType.Word or ContentType.Fingering => SpecialContentType.Fingering | SpecialContentType.Text,
						_ => SpecialContentType.None,
					};

				//Rhythmen können mit Leerzeichen, Punctuation oder Wörtern kombiniert werden
				case ContentType.Rhythm:
					if (offset <= ContentOffset.Zero || offset > leftLength)
						return SpecialContentType.None;

					if (offset == leftLength)
						return right.Type switch
						{
							ContentType.Punctuation or ContentType.Word => SpecialContentType.Rhythm,
							_ => SpecialContentType.None,
						};

					return right.Type switch
					{
						ContentType.Space or ContentType.Punctuation or ContentType.Word => SpecialContentType.Rhythm,
						_ => SpecialContentType.None,
					};

				//Akkorde können nur kombiniert werden, wenn dabei Akkorde oder Text herauskommen
				case ContentType.Chord:
					return right.Type switch
					{
						ContentType.Word or ContentType.Punctuation or ContentType.Fingering => SpecialContentType.Chord | SpecialContentType.Text,
						_ => SpecialContentType.None,
					};

				//An Punctuations können nur Punctuations angehängt werden
				case ContentType.Punctuation:
					return right.Type == ContentType.Punctuation ? SpecialContentType.Text : SpecialContentType.None;

				//An Wörter kann alles außer Leerzeichen oder Punctuations angehängt werden
				case ContentType.Word:
					return right.Type switch
					{
						ContentType.Word or ContentType.Chord or ContentType.Fingering => SpecialContentType.Text | SpecialContentType.Chord | SpecialContentType.Fingering | SpecialContentType.Rhythm,
						_ => SpecialContentType.None,
					};

				default:
					return SpecialContentType.None;
			}
		}

		private static bool CanAppendTo(ComponentContent left, ComponentContent right, ComponentContent total)
		{
			//Zwei Akkorde oder ein Akkord und ein Fingering können nicht kombiniert werden
			if ((left.Type, right.Type) is (ContentType.Chord, ContentType.Fingering) or (ContentType.Fingering, ContentType.Chord) or (ContentType.Chord, ContentType.Chord))
				return false;

			//Fingerings können mit Punctionations kombiniert werden, wenn dabei Fingerings herauskommen
			if ((left.Type, right.Type) is (ContentType.Fingering, ContentType.Punctuation) or (ContentType.Punctuation, ContentType.Fingering))
				return total.Type == ContentType.Fingering;

			//Spaces oder Punctuations können nur untereinander kombiniert werden
			if (left.Type is ContentType.Space or ContentType.Punctuation)
				return right.Type == left.Type;
			if (right.Type is ContentType.Space or ContentType.Punctuation)
				return right.Type == left.Type;

			return true;
		}
		#endregion

		#region Operators
		public string? ToString(ISheetFormatter? formatter) => Content.ToString(formatter);

		public override string? ToString() => Content.ToString();
		#endregion

		public abstract class Attachment : SheetLineComponent
		{
			public ContentOffset Offset { get; protected set; }

			public abstract bool IsEmpty { get; }
			internal abstract RenderBounds RenderBounds { get; private protected set; }

			protected Attachment(ContentOffset offset)
			{
				if (offset < ContentOffset.Zero)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal void SetOffset(ContentOffset offset)
			{
				if (offset < ContentOffset.Zero)
					throw new ArgumentOutOfRangeException(nameof(offset));

				Offset = offset;
			}

			internal abstract SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter);

			public abstract Stored Store();

			public readonly record struct Stored : ISelfStored<Stored, Attachment>
			{
				private readonly ContentOffset offset;
				private readonly ComponentContent content;

				internal Stored(ContentOffset offset, ComponentContent content)
				{
					this.offset = offset;
					this.content = content;
				}

				public Attachment Restore()
					=> new VarietyAttachment(offset, content);
			}
		}

		public sealed class VarietyAttachment : Attachment
		{
			internal ComponentContent Content { get; private set; }

			public override bool IsEmpty => Content.IsEmpty;
			internal override RenderBounds RenderBounds { get; private protected set; }

			public VarietyAttachment(ContentOffset offset, string text)
				: base(offset)
			{
				Content = new(text);
			}

			public VarietyAttachment(ContentOffset offset, Chord chord)
				: base(offset)
			{
				Content = new(chord);
			}

			internal VarietyAttachment(ContentOffset offset, ComponentContent content)
				: base(offset)
			{
				Content = content;
			}

			public static VarietyAttachment FromString(ContentOffset offset, string content, ISheetEditorFormatter? formatter)
				=> new VarietyAttachment(offset, ComponentContent.FromString(content, formatter));

			internal override SheetDisplayLineElement? CreateDisplayAttachment(out Action<RenderBounds> setRenderBounds, ISheetFormatter? formatter)
			{
				setRenderBounds = SetRenderBounds;
				return Content.CreateElement(new(this, Offset), formatter) with
				{
					Tags = [SheetDisplayTag.Attachment]
				};
			}

			private void SetRenderBounds(RenderBounds bounds)
				=> RenderBounds = bounds;

			public override Stored Store() => new(Offset, Content);

			#region Editing
			internal Action? TryRemoveContent(ContentOffset offset, ContentOffset length, ISheetEditorFormatter? formatter)
			{
				//Entferne den Inhalt
				var newContent = Content.RemoveContent(offset, length, SpecialContentType.All, formatter);
				if (newContent is null)
					return null;

				//Setze den neuen Inhalt
				return () => Content = newContent.Value;
			}

			internal void InsertContent(string content, ContentOffset offset, ISheetEditorFormatter? formatter)
			{
				//Füge Inhalt ein
				Content = Content.MergeContents(content, offset, SpecialContentType.All, formatter).NewContent;
			}

			internal void ReplaceContent(string content, ISheetEditorFormatter? formatter)
			{
				//Ersetze Inhalt
				Content = ComponentContent.FromString(content, formatter, SpecialContentType.All);
			}

			internal VarietyAttachment? SplitEnd(ContentOffset offset, ISheetEditorFormatter? formatter)
			{
				//Trenne den Inhalt
				var newContent = Content.SplitEnd(offset, SpecialContentType.All, SpecialContentType.All, formatter);
				if (newContent is null) return null;
				Content = newContent.Value.NewContent;

				//Erzeuge das neue Ende
				var newEnd = new VarietyAttachment(Offset + offset, newContent.Value.EndContent);
				return newEnd;
			}

			internal bool TryMergeContents(VarietyAttachment attachment, ISheetEditorFormatter? formatter)
			{
				//Füge Inhalt hinzu
				Content = Content.MergeContents(attachment.Content, ContentOffset.FarEnd, SpecialContentType.All, formatter).NewContent;
				return true;
			}
			#endregion
		}
	}
}
