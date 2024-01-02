using Skinnix.RhymeTool.Data.Structure.Display;

namespace Skinnix.RhymeTool.Data.Structure;

public class SheetCompositeLine : SheetLine
{
	public List<SheetCompositeLineComponent> Components { get; } = new();

	public SheetCompositeLine() { }

	public SheetCompositeLine(params SheetCompositeLineComponent[] components) : this((IEnumerable<SheetCompositeLineComponent>)components) { }
	public SheetCompositeLine(IEnumerable<SheetCompositeLineComponent> components)
	{
		Components = components.ToList();
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetFormatter? formatter = null)
	{
		//Erzeuge die Blöcke
		var blocks = Components.SelectMany(c => c.CreateBlocks()).ToList();

		//Erzeuge Zeilen aus den Blöcken
		var lines = new List<SheetDisplayLineBuilder>();
		foreach (var block in blocks)
		{
			//Können alle Zeilenblöcke ohne Überschneidungen angehängt werden?
			var minOffset = int.MaxValue;
			var maxOffset = int.MinValue;
			foreach (var lineBlock in block.Lines)
			{
				foreach (var line in lines)
				{
					if (lineBlock.CanAppend(line))
					{
						minOffset = Math.Min(minOffset, line.CurrentLength);
						maxOffset = Math.Max(maxOffset, line.CurrentLength);
					}
				}
			}
			if (maxOffset < minOffset)
				minOffset = maxOffset = 0;

			//Gehe durch die Zeilenblöcke
			foreach (var lineBlock in block.Lines)
			{
				//Gehe durch alle Zeilen
				var appended = false;
				foreach (var line in lines)
				{
					//Versuche den Block hinzuzufügen
					if (!appended && lineBlock.CanAppend(line))
					{
						//Berechne Mindestabstand
						var minSpace = formatter?.SpaceBefore(this, line, lineBlock.Element) ?? 0;

						//Verlängere ggf. die Zeile
						line.ExtendLength(maxOffset, minSpace);
						appended = true;

						//Hänge den Block an
						line.Append(lineBlock.Element, formatter);
						break;
					}
				}

				//Keine passende Zeile gefunden?
				if (!appended)
				{
					//Erzeuge einen neuen Zeilenbuilder
					var line = lineBlock.CreateBuilder();
					lines.Add(line);

					//Berechne Mindestabstand
					var minSpace = formatter?.SpaceBefore(this, line, lineBlock.Element) ?? 0;

					//Verlängere ggf. die Zeile
					line.ExtendLength(maxOffset, minSpace);

					//Hänge den Block an
					line.Append(lineBlock.Element, formatter);
					appended = true;
				}
			}
		}

		//Sortiere und erzeuge die Displayzeilen
		lines.Sort();
		return lines.Select(l => l.CreateDisplayLine());
	}

	public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null)
	{
		//Erzeuge die Blöcke
		var blocks = Components.SelectMany(c => c.CreateBlocks()).ToList();

		//Erzeuge und zähle virtuelle Zeilen
		var lines = new List<SheetDisplayLineBuilder>();
		foreach (var block in blocks)
		{
			//Gehe durch die Zeilenblöcke
			foreach (var lineBlock in block.Lines)
			{
				//Gehe durch alle Zeilen
				var found = false;
				foreach (var line in lines)
				{
					//Versuche den Block hinzuzufügen
					if (!found && lineBlock.CanAppend(line))
					{
						found = true;
						break;
					}
				}

				//Keine passende Zeile gefunden?
				if (!found)
				{
					//Erzeuge eine neue virtuelle Zeile
					var lineBuilder = lineBlock.CreateBuilder();
					lines.Add(lineBuilder);
				}
			}
		}

		//Sortiere die Zeilen
		lines.Sort();

		//Erzeuge Blöcke mit der passenden Anzahl Zeilen
		foreach (var block in blocks)
		{
			//Erzeuge einen Block aus den Zeilenblöcken
			var blockLines = new SheetDisplayLine?[lines.Count];

			//Gehe durch die Zeilenblöcke
			foreach (var lineBlock in block.Lines)
			{
				//Finde die passende Zeile
				var i = 0;
				foreach (var targetLine in lines)
				{
					if (lineBlock.CanAppend(targetLine))
					{
						//Erzeuge eine Zeile an der passenden Position
						var line = lineBlock.CreateBuilderAndAppend(0, formatter).CreateDisplayLine();
						blockLines[i] = line;
						break;
					}

					i++;
				}
			}

			//Fülle Nullwerte mit leeren Zeilen auf
			for (var i = 0; i < blockLines.Length; i++)
			{
				if (blockLines[i] == null)
					blockLines[i] = lines[i].CreateDisplayLine();
			}

			yield return new SheetDisplayContentBlock(blockLines!);
		}
	}
}

public abstract class SheetCompositeLineComponent
{
	public abstract IEnumerable<SheetCompositeLineBlock> CreateBlocks();
}

public class SheetChordedWord : SheetCompositeLineComponent
{
	public List<WordComponent> Components { get; } = new();

	public SheetChordedWord(params WordComponent[] components) : this((IEnumerable<WordComponent>)components) { }
	public SheetChordedWord(IEnumerable<WordComponent> components)
	{
		Components = components.ToList();
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
	{
		foreach (var component in Components)
		{
			var attachmentBlocks = component.Attachments.Select(a => a.CreateDisplayBlockLine()).ToList();

			//Textanker, wenn es Attachments gibt
			SheetDisplayElement textElement = attachmentBlocks.Count == 0
				? new SheetDisplayText(component.Text)
				: new SheetDisplayAnchorText(component.Text)
				{
					Targets = attachmentBlocks.Select(b => b.Element).ToList()
				};
			var textBlock = SheetCompositeLineBlockLine.Create<SheetDisplayTextLine.Builder>(textElement);

			yield return new SheetCompositeLineBlock(attachmentBlocks.Prepend(textBlock));
		}
	}
}

public class WordComponent
{
	public string Text { get; set; }

	public List<WordComponentAttachment> Attachments { get; } = new();

	public WordComponent(string text)
	{
		Text = text;
	}
}

public abstract class WordComponentAttachment
{
	public int Offset { get; set; }

	public abstract object GetAttachment();
	public abstract SheetCompositeLineBlockLine CreateDisplayBlockLine();
}

public class WordComponentChord : WordComponentAttachment
{
	public Chord Chord { get; set; }

	public WordComponentChord(Chord chord)
	{
		Chord = chord;
	}

	public override Chord GetAttachment() => Chord;
	public override SheetDisplayComponentBlockLine<SheetDisplayChordLine.Builder> CreateDisplayBlockLine()
		=> SheetCompositeLineBlockLine.Create<SheetDisplayChordLine.Builder>(new SheetDisplayChord(Chord));
}

public class WordComponentText : WordComponentAttachment
{
	public string Text { get; set; }

	public WordComponentText(string text)
	{
		Text = text;
	}

	public override string GetAttachment() => Text;
	public override SheetDisplayComponentBlockLine<SheetDisplayChordLine.Builder> CreateDisplayBlockLine()
		=> SheetCompositeLineBlockLine.Create<SheetDisplayChordLine.Builder>(new SheetDisplayText(Text));
}

public class SheetSpace : SheetCompositeLineComponent
{
	public int Count { get; set; }

	public SheetSpace(int count = 1)
	{
		Count = count;
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
		=> new SheetCompositeLineBlock[]
		{
			new(SheetCompositeLineBlockLine.Create<SheetDisplayTextLine.Builder>(new SheetDisplaySpace(Count)))
		};
}