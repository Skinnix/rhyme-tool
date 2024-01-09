using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetCompositeLine : SheetLine
{
	public ModifiableObservableCollection<SheetCompositeLineComponent> Components { get; }

	public SheetCompositeLine()
	{
		Components = Register(new ModifiableObservableCollection<SheetCompositeLineComponent>());
	}

	public SheetCompositeLine(params SheetCompositeLineComponent[] components) : this((IEnumerable<SheetCompositeLineComponent>)components) { }
	public SheetCompositeLine(IEnumerable<SheetCompositeLineComponent> components)
	{
		Components = Register(new ModifiableObservableCollection<SheetCompositeLineComponent>(components));
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
		return lines.Select(l => EditingAdapter.Create(this, l));
	}

	private class EditingAdapter : ISheetDisplayLineEditing
	{
		private readonly SheetCompositeLine owner;
		private readonly SheetDisplayLine line;

		private EditingAdapter(SheetCompositeLine owner, SheetDisplayLineBuilder builder)
		{
			this.owner = owner;
			this.line = builder.CreateDisplayLine(this);
		}

		public static SheetDisplayLine Create(SheetCompositeLine owner, SheetDisplayLineBuilder builder) => new EditingAdapter(owner, builder).line;

		public bool InsertContent(string content, int selectionStart, int selectionEnd, ISheetFormatter? formatter) => throw new NotImplementedException();
		public bool DeleteContent(int selectionStart, int selectionEnd, ISheetFormatter? formatter, bool forward = false)
		{
			if (line is SheetDisplayTextLine)
			{
				//Finde die Selection
				var selectionLength = selectionEnd - selectionStart;
				if (selectionLength != 0)
				{
					//Finde alle Elemente, die in der Selection liegen
					var selectedElements = line.GetElementsIn(selectionStart, selectionEnd, formatter).ToList();
					foreach ((var offset, var element) in selectedElements)
					{
						//Liegt das Element komplett in der Selection?
						var elementLength = element.GetLength(formatter);
						if (offset >= selectionStart && offset + elementLength <= selectionEnd)
						{
							//Lösche das Element
							if (element.Source is SheetCompositeLineComponent compositeLineComponent)
								owner.Components.Remove(compositeLineComponent);
							continue;
						}

						//Überschneidet sich das Element mit der Selection?
						var elementOverlapsStart = offset < selectionStart && offset + elementLength > selectionStart;
						var elementOverlapsEnd = offset < selectionEnd && offset + elementLength > selectionEnd;

						//Berechne Länge der Kürzung
						var cutOffset = 0;
						var cutLength = selectionEnd - selectionStart;
						if (elementOverlapsStart)
						{
							cutOffset = selectionStart - offset;
							cutLength -= cutOffset;
						}
						if (elementOverlapsEnd)
						{
							cutLength -= offset + elementLength - selectionEnd;
						}

						if (cutLength > 0)
						{
							//Kürze das Element
							if (element.Source != null)
							{
								var cutResult = element.Source.CutContent(cutOffset, cutLength, formatter);

								//Entferne oder ersetze ggf. das Element
								if (element.Source is SheetCompositeLineComponent compositeLineComponent)
								{
									if (cutResult.Replacement is SheetCompositeLineComponent replacement)
									{
										owner.Components.Replace(compositeLineComponent, replacement);
									}
									else if (cutResult.Remove)
									{
										owner.Components.Remove((SheetCompositeLineComponent)element.Source);
									}
								}
							}
						}
					}
				}
			}

			return true;
		}
	}

	//public override IEnumerable<SheetDisplayBlock> CreateDisplayBlocks(ISheetFormatter? formatter = null)
	//{
	//    //Erzeuge die Blöcke
	//    var blocks = Components.SelectMany(c => c.CreateBlocks()).ToList();

	//    //Erzeuge und zähle virtuelle Zeilen
	//    var lines = new List<SheetDisplayLineBuilder>();
	//    foreach (var block in blocks)
	//    {
	//        //Gehe durch die Zeilenblöcke
	//        foreach (var lineBlock in block.Lines)
	//        {
	//            //Gehe durch alle Zeilen
	//            var found = false;
	//            foreach (var line in lines)
	//            {
	//                //Versuche den Block hinzuzufügen
	//                if (!found && lineBlock.CanAppend(line))
	//                {
	//                    found = true;
	//                    break;
	//                }
	//            }

	//            //Keine passende Zeile gefunden?
	//            if (!found)
	//            {
	//                //Erzeuge eine neue virtuelle Zeile
	//                var lineBuilder = lineBlock.CreateBuilder();
	//                lines.Add(lineBuilder);
	//            }
	//        }
	//    }

	//    //Sortiere die Zeilen
	//    lines.Sort();

	//    //Erzeuge Blöcke mit der passenden Anzahl Zeilen
	//    foreach (var block in blocks)
	//    {
	//        //Erzeuge einen Block aus den Zeilenblöcken
	//        var blockLines = new SheetDisplayLine[lines.Count];

	//        //Gehe durch die Zeilenblöcke
	//        foreach (var lineBlock in block.Lines)
	//        {
	//            //Finde die passende Zeile
	//            var i = 0;
	//            foreach (var targetLine in lines)
	//            {
	//                if (lineBlock.CanAppend(targetLine))
	//                {
	//                    //Erzeuge eine Zeile an der passenden Position
	//                    var line = lineBlock.CreateBuilderAndAppend(0, formatter).CreateDisplayLine();
	//                    blockLines[i] = line;
	//                    break;
	//                }

	//                i++;
	//            }
	//        }

	//        //Fülle Nullwerte mit leeren Zeilen auf
	//        for (var i = 0; i < blockLines.Length; i++)
	//        {
	//            if (blockLines[i] == null)
	//                blockLines[i] = lines[i].CreateDisplayLine();
	//        }

	//        yield return new SheetDisplayContentBlock(blockLines!);
	//    }
	//}
}

public abstract class SheetCompositeLineComponent : SheetLineComponent
{
	public abstract IEnumerable<SheetCompositeLineBlock> CreateBlocks();
}

public sealed class SheetChord : SheetCompositeLineComponent
{
	private Chord chord;
	public Chord Chord
	{
		get => chord;
		set => Set(ref chord, value);
	}

	private string? suffix;
	public string? Suffix
	{
		get => suffix;
		set => Set(ref suffix, value);
	}

	public SheetChord(Chord chord)
	{
		this.chord = chord;
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
	{
		yield return new SheetCompositeLineBlock(
			new SheetCompositeLineBlockRow<SheetDisplayChordLine.Builder>(
				new SheetDisplayLineChord(this, Chord)));

		if (Suffix != null)
		{
			yield return new SheetCompositeLineBlock(
				new SheetCompositeLineBlockRow<SheetDisplayChordLine.Builder>(
					new SheetDisplayLineText(this, Suffix)));
		}
	}

	public override SheetLineComponentCutResult CutContent(int cutOffset, int cutLength, ISheetFormatter? formatter)
	{
		throw new NotImplementedException();
		var chordString = Chord.ToString(formatter);
		if (cutOffset >= chordString.Length)
		{
			//Kürze Suffix
			var suffixOffset = cutOffset - chordString.Length;
			var suffixLength = Math.Min(cutLength, Suffix!.Length - suffixOffset);
		}
	}
}

public sealed class SheetSpace : SheetCompositeLineComponent
{
	private int length;
	public int Length
	{
		get => length;
		set => Set(ref length, value);
	}

	public SheetSpace(int length = 1)
	{
		this.length = length;
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
		=> new SheetCompositeLineBlock[]
		{
			new SheetCompositeLineBlock(
				new SheetCompositeLineBlockRow<SheetDisplayTextLine.Builder>(
					new SheetDisplayLineSpace(this, Length)))
		};

	public override SheetLineComponentCutResult CutContent(int cutOffset, int cutLength, ISheetFormatter? formatter)
	{
		Length -= cutLength;
		return new SheetLineComponentCutResult()
		{
			Remove = Length <= 0
		};
	}
}

public sealed class SheetComplexWord : SheetCompositeLineComponent
{
	public ModifiableObservableCollection<WordComponent> Components { get; }

	public SheetComplexWord()
	{
		Components = Register(new ModifiableObservableCollection<WordComponent>());
	}

	public SheetComplexWord(params WordComponent[] components) : this((IEnumerable<WordComponent>)components) { }
	public SheetComplexWord(IEnumerable<WordComponent> components)
	{
		Components = Register(new ModifiableObservableCollection<WordComponent>(components));
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
	{
		foreach (var component in Components)
		{
			var attachmentRows = component.Attachments.Select(a => a.CreateDisplayBlockLine()).ToList();

			//Textanker, wenn es Attachments gibt
			SheetDisplayLineElement textElement = attachmentRows.Count == 0
				? new SheetDisplayLineText(this, component.Text)
				: new SheetDisplayLineAnchorText(this, component.Text)
				{
					Targets = attachmentRows.Select(b => b.Element).ToList()
				};
			var textRow = new SheetCompositeLineBlockRow<SheetDisplayTextLine.Builder>(textElement);

			yield return new SheetCompositeLineBlock(attachmentRows.Prepend(textRow));
		}
	}

	public override SheetLineComponentCutResult CutContent(int cutOffset, int cutLength, ISheetFormatter? formatter)
	{
		foreach (var component in Components)
		{
			if (cutLength <= 0) break;

			//Ist der Offset noch nicht erreicht?
			if (cutOffset > 0)
			{
				//Ist die Komponente kürzer als Offset?
				if (component.Length <= cutOffset)
				{
					//Überspringe die Komponente
					cutOffset -= component.Length;
					continue;
				}

				//Fällt die Komponente komplett in die Kürzung?
				if (component.Length <= cutOffset + cutLength)
				{
					//Kürze die Komponente von rechts
					component.Text = component.Text[0..cutOffset];
					cutLength -= component.Length - cutOffset;
					cutOffset = 0;
					continue;
				}
				else
				{
					//Schneide einen Teil aus der Komponente heraus
					component.Text = component.Text[0..cutOffset] + component.Text[(cutOffset + cutLength)..];
					break;
				}
			}

			//Ist die Komponente länger?
			if (component.Length > cutLength)
			{
				//Kürze die Komponente
				component.Text = component.Text[cutLength..];
				break;
			}

			//Kürze die Komponente von links
			component.Text = component.Text[cutOffset..];
			cutLength -= cutOffset;
			cutOffset = 0;
			continue;
		}

		return new SheetLineComponentCutResult()
		{
			Remove = Components.Count == 0
		};
	}
}