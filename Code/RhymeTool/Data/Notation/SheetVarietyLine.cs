using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetVarietyLine : SheetLine
{
	private readonly List<Component> components = new();

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null) => throw new NotImplementedException();

	public enum ComponentType
	{
		Word,
		Chord,
	}

	public abstract class Component
	{
		public ComponentType Type { get; protected set; }

		protected Component(ComponentType type)
		{
			Type = type;
		}

		public abstract void BuildLines(LineBuilders builders);

		public record struct LineBuilders(SheetDisplayTextLine.Builder TextLine, SheetDisplayChordLine.Builder ChordLine)
		{
			public int CurrentLength => Math.Max(TextLine.CurrentLength, ChordLine.CurrentLength);
		}
	}

	public class Word : Component, ISheetDisplayLineElementSource
	{
		private List<DisplayBlock>? display;

		private readonly List<Attachment> attachments = new();
		public IReadOnlyList<Attachment> Attachments => attachments;

		public string Text { get; private set; }

		public Word(string text)
			: base(ComponentType.Word)
		{
			Text = text;
		}

		#region Display
		public override void BuildLines(LineBuilders builders)
		{
			//Stelle sicher, dass die Textzeile lang genug ist
			builders.TextLine.ExtendLength(builders.CurrentLength, 0);

			//Füge das Wort hinzu
			builders.TextLine.Append(display);
		}

		private List<DisplayBlock> GetDisplay()
		{
			if (display is null)
			{
				display = new List<DisplayBlock>();

				//Trenne den Text an Attachments
				Attachment? previousAttachment = null;
				foreach (var attachment in attachments.Append<Attachment?>(null))
				{
					//Erzeuge einen Block für das vorherige Attachment und füge ihn hinzu
					var previousAttachmentOffset = previousAttachment?.Offset ?? 0;
					var attachmentText = attachment is null ? Text[previousAttachmentOffset..]
						: Text[previousAttachmentOffset..attachment.Offset];
					var block = previousAttachment?.CreateBlock(attachmentText)
						?? new DisplayBlock(new SheetDisplayLineAnchorText(this, attachmentText));
					display.Add(block);

					//Merke das aktuelle Attachment
					previousAttachment = attachment;
				}
			}
		}

		internal record DisplayBlock(SheetDisplayLineAnchorText Text)
		{
			
		}
		#endregion

		public abstract class Attachment : ISheetDisplayLineElementSource
		{
			public int Offset { get; protected set; }

			protected Attachment(int offset)
			{
				Offset = offset;
			}

			public abstract int GetLength(ISheetFormatter? formatter = null);
			internal abstract DisplayBlock? CreateBlock(string text);
		}

		public sealed class ChordAttachment : Attachment
		{
			public string? Text { get; private set; }
			public Chord? Chord { get; private set; }

			public ChordAttachment(int offset, string text)
				: base(offset)
			{
				Text = text;
			}

			public ChordAttachment(int offset, Chord chord)
				: base(offset)
			{
				Chord = chord;
				Text = Chord.ToString();
			}

			public override int GetLength(ISheetFormatter? formatter = null)
				=> Chord?.ToString(formatter).Length ?? Text?.Length ?? 0;

			internal override DisplayBlock? CreateBlock(string text)
			{
				//Erzeuge das Element für den Akkord
				SheetDisplayLineElement? chordElement = Chord is not null ? new SheetDisplayLineChord(this, Chord)
					: Text is not null ? new SheetDisplayLineAnchorText(this, Text)
					: null;
				if (chordElement is null) return null;

				//Erzeuge den Block
				return new DisplayBlock(new SheetDisplayLineAnchorText)
			}
		}
	}
}
