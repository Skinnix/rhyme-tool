using Skinnix.RhymeTool.Data.Structure.Display;

namespace Skinnix.RhymeTool.Data.Structure;

public abstract class SheetChordedLineComponent
{
	public abstract IEnumerable<SheetDisplayComponentBlock> CreateDisplayBlocks();
}

public class SheetChordedLine : SheetLine
{
	public List<SheetChordedLineComponent> Components { get; } = new();

	public SheetChordedLine() { }

	public SheetChordedLine(params SheetChordedLineComponent[] components) : this((IEnumerable<SheetChordedLineComponent>)components) { }
	public SheetChordedLine(IEnumerable<SheetChordedLineComponent> components)
	{
		Components = components.ToList();
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines()
	{
		//Erzeuge die Blöcke
		var blocks = Components.SelectMany(c => c.CreateDisplayBlocks()).ToList();

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
						//Verlängere ggf. die Zeile
						line.ExtendLength(maxOffset);
						appended = true;

						//Hänge den Block an
						line.Append(lineBlock.Element);
						break;
					}
				}

				//Keine passende Zeile gefunden?
				if (!appended)
				{
					//Erzeuge einen neuen Zeilenbuilder (mit passendem Offset)
					var lineBuilder = lineBlock.CreateBuilderAndAppend(maxOffset);
					lines.Add(lineBuilder);
				}
			}
		}

		//Sortiere und erzeuge die Displayzeilen
		lines.Sort();
		return lines.Select(l => l.CreateDisplayLine());
	}

	public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks()
	{
		//Erzeuge die Blöcke
		var blocks = Components.SelectMany(c => c.CreateDisplayBlocks()).ToList();

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
						var line = lineBlock.CreateBuilderAndAppend(0).CreateDisplayLine();
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

public class SheetChordedLineWord : SheetChordedLineComponent
{
	public List<WordComponent> Components { get; } = new();

	public SheetChordedLineWord(params WordComponent[] components) : this((IEnumerable<WordComponent>)components) { }
	public SheetChordedLineWord(IEnumerable<WordComponent> components)
	{
		Components = components.ToList();
	}

	public override IEnumerable<SheetDisplayComponentBlock> CreateDisplayBlocks()
	{
		foreach (var component in Components)
		{
			var textBlock = SheetDisplayComponentBlockLine.Create<SheetDisplayTextLine.Builder>(new SheetDisplayText(component.Text));
			var attachmentBlocks = component.Attachments.Select(a => a.CreateDisplayLineBlock());
			yield return new SheetDisplayComponentBlock(attachmentBlocks.Prepend(textBlock));
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
	public abstract SheetDisplayComponentBlockLine CreateDisplayLineBlock();
}

public class WordComponentChord : WordComponentAttachment
{
	public Chord Chord { get; set; }

	public WordComponentChord(Chord chord)
	{
		Chord = chord;
	}

	public override Chord GetAttachment() => Chord;
	public override SheetDisplayComponentBlockLine<SheetDisplayChordLine.Builder> CreateDisplayLineBlock()
		=> SheetDisplayComponentBlockLine.Create<SheetDisplayChordLine.Builder>(new SheetDisplayChord(Chord));
}

public class WordComponentText : WordComponentAttachment
{
	public string Text { get; set; }

	public WordComponentText(string text)
	{
		Text = text;
	}

	public override string GetAttachment() => Text;
	public override SheetDisplayComponentBlockLine<SheetDisplayChordLine.Builder> CreateDisplayLineBlock()
		=> SheetDisplayComponentBlockLine.Create<SheetDisplayChordLine.Builder>(new SheetDisplayText(Text));
}

public class SheetChordedLineSpace : SheetChordedLineComponent
{
	public int Count { get; set; }

	public SheetChordedLineSpace(int count = 1)
	{
		Count = count;
	}

	public override IEnumerable<SheetDisplayComponentBlock> CreateDisplayBlocks()
		=> new SheetDisplayComponentBlock[]
		{
			new(SheetDisplayComponentBlockLine.Create<SheetDisplayTextLine.Builder>(new SheetDisplaySpace(Count)))
		};
}