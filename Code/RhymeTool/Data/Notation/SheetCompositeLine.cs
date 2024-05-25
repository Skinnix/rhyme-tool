using System.Windows.Markup;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetCompositeLine : SheetLine
{
	public ModifiableObservableCollectionWithParent<SheetCompositeLineComponent, SheetCompositeLine> Components { get; }

	public SheetCompositeLine()
	{
		Components = Register(new ModifiableObservableCollectionWithParent<SheetCompositeLineComponent, SheetCompositeLine>(this));
	}

	public SheetCompositeLine(params SheetCompositeLineComponent[] components) : this((IEnumerable<SheetCompositeLineComponent>)components) { }
	public SheetCompositeLine(IEnumerable<SheetCompositeLineComponent> components)
	{
		Components = Register(new ModifiableObservableCollectionWithParent<SheetCompositeLineComponent, SheetCompositeLine>(this, components));
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

		//public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter)
		//{
		//	if (line is SheetDisplayTextLine)
		//	{
		//		//Finde die Elemente in, direkt links und direkt rechts von der Selection
		//		var elements = line.GetElementsIn(new SimpleRange(selectionRange.Start - 1, selectionRange.End + 1), formatter).ToList();

		//		//Finde ggf. ein Element vor der Selection
		//		var leftOutsideElement = elements.FirstOrDefault(e => e.Offset + e.Length < selectionRange.Start);

		//		//Finde ggf. ein Element, das mit der Selection beginnt
		//		var leftInsideElement = elements.FirstOrDefault(e => e.Offset == selectionRange.Start);

		//		//Finde ggf. ein Element, das mit der Selection endet
		//		var rightInsideElement = elements.FirstOrDefault(e => e.Offset + e.Length == selectionRange.End);

		//		//Finde ggf. ein Element nach der Selection
		//		var rightOutsideElement = elements.FirstOrDefault(e => e.Offset > selectionRange.End);

		//		//Falls alle 4 gesetzt sind, ignoriere rightInsideElement
		//		if (leftOutsideElement.Element != null && leftInsideElement.Element != null && rightInsideElement.Element != null && rightOutsideElement.Element != null)
		//			rightInsideElement = default;

		//		//Jetzt gibt es maximal 3 relevante Elemente
		//		var relevantElements = new[] { leftOutsideElement, leftInsideElement, rightInsideElement, rightOutsideElement }.Where(e => e.Element != null).ToList();

		//		//Gibt es gar kein relevantes Element?
		//		if (relevantElements.Count == 0)
		//		{
		//			//Füge den Inhalt am Ende der Zeile ein
		//			var totalLength = line.GetElements().Sum(e => e.GetLength(formatter));

		//			//Füge ggf. Leerzeichen ein
		//			var spaceLength = selectionRange.Start - totalLength - 1;
		//			if (spaceLength > 0)
		//				owner.Components.Add(new SheetSpace(spaceLength));

		//			//Füge das Element ein
		//			var element = CreateElementFromContent(content);
		//			owner.Components.Add(element);
		//			var endOffset = selectionRange.Start + content.Length;
		//			return new LineEditResult(true, new SimpleRange(endOffset, endOffset));
		//		}

		//		//Prüfe zwischen diesen 3 Elementen, welches erweitert werden soll
		//		(var targetElement, var targetOffset, var targetLength) = CheckExtendWhichComponent(relevantElements, content);

		//		//Modifiziere das Element
		//		var inserted = false;
		//		if (targetElement != null)
		//		{
		//			//Berechne den zu modifizierenden Bereich
		//			var startOffset = Math.Max(selectionRange.Start - targetOffset, 0);
		//			var endOffset = Math.Min(selectionRange.End - targetOffset, targetLength);
		//			var replaceRange = new SimpleRange(startOffset, endOffset);

		//			//Ersetze Inhalt in dem Bereich
		//			var insertResult = targetElement.InsertContent(insertOffset, content, formatter);
		//			if (insertResult.Success)
		//				inserted = true;
		//		}

		//		//Lösche oder kürze ggf. Elemente in der Selection, die nicht erweitert wurden
		//		foreach ((var offset, var elementLength, var element) in elements)
		//		{
					

		//			//Liegt der Start der Selection in dem Element?
		//			if (!inserted && (overlap == RangeOverlap.OverlapsStart || offset == selectionRange.Start))
		//			{
		//				//Falls die Selection genau zwischen den Elementen beginnt, entscheide, welches Element erweitert wird
		//				var targetElement = element.Source as SheetCompositeLineComponent;
		//				if (leftElement != null)
		//					targetElement = CheckExtendWhichComponent(leftElement, element, content);

		//				//Füge den Inhalt in das Element ein
		//				if (targetElement != null)
		//				{
		//					targetElement.InsertContent(selectionRange.Start - offset, content, formatter);
		//				}

		//				inserted = true;
		//				continue;
		//			}

		//			//Liegt das Element komplett in der Selection?
		//			if (overlap == RangeOverlap.InsideRange)
		//			{
		//				//Lösche das Element
		//				if (element.Source is SheetCompositeLineComponent compositeLineComponent)
		//					owner.Components.Remove(compositeLineComponent);
		//				continue;
		//			}

		//			//Überschneidet sich das Element mit der Selection?
		//			if ((overlap | RangeOverlap.OverlapFlag) != 0)
		//			{
		//				//Berechne Länge und Offset der Kürzung
		//				var cutStartOffset = Math.Max(offset, selectionRange.Start);
		//				var cutEndOffset = Math.Min(offset + elementLength, selectionRange.End);
		//				var cutOffset = cutStartOffset - offset;
		//				var cutLength = cutEndOffset - cutStartOffset;

		//				if (cutLength > 0)
		//				{
		//					//Kürze das Element
		//					if (element.Source != null)
		//					{
		//						var cutResult = element.Source.CutContent(cutOffset, cutLength, formatter);

		//						//Entferne oder ersetze ggf. das Element
		//						if (element.Source is SheetCompositeLineComponent compositeLineComponent)
		//						{
		//							if (cutResult.Replacement is SheetCompositeLineComponent replacement)
		//							{
		//								owner.Components.Replace(compositeLineComponent, replacement);
		//							}
		//							else if (cutResult.Remove)
		//							{
		//								owner.Components.Remove((SheetCompositeLineComponent)element.Source);
		//							}
		//						}
		//					}
		//				}
		//			}
		//		}
		//	}
		//}

		private void OptimizeRow(ISheetFormatter? formatter, IReadOnlyCollection<SheetCompositeLineComponent>? modifiedElements, SheetCompositeLineComponent? newElement = null)
		{
			//Zuerst werden leere Wörter an die darauffolgenden angefügt. Dabei werden alle Elemente optimiert und ggf. aufgetrennt).
			//Danach werden leere Wörter gelöscht und die Optimierungen (Löschungen und Einfügungen) ausgeführt.
			//Schließlich werden Wörter, die zusammengefügt werden können, zusammengefügt.

			//Füge leere Wörter an die darauffolgenden an
			var optimizations = new List<(int Index, SheetCompositeLineComponentOptimizationResult Optimization)>();
			SheetComplexWord? mergeWord = null;
			var index = -1;
			foreach (var component in owner.Components)
			{
				index++;
				if (component is SheetComplexWord word)
				{
					//Füge ggf. das vorherige leere Wort an dieses an
					if (mergeWord != null)
					{
						//Füge das leere Wort an
						word.Append(mergeWord);
					}
					else
					{
						//ist das Wort leer?
						word.CheckFeatures(out var zeroLength, out var hasAttachments, out _);
						if (zeroLength)
						{
							//Hat es trotzdem Attachments?
							if (hasAttachments)
							{
								//Merke leere Wörter mit Attachments für den nächsten Durchgang
								mergeWord = word;
								continue;
							}
							else
							{
								//Entferne komplett leere Wörter
								optimizations.Add((index, new SheetCompositeLineComponentOptimizationResult(word) { RemoveComponent = true }));
								continue;
							}
						}
					}
				}

				//Lösche das leere Wort
				if (mergeWord != null)
				{
					//Lösche das leere Wort später
					optimizations.Add((index, new SheetCompositeLineComponentOptimizationResult(mergeWord) { RemoveComponent = true }));
					mergeWord = null;
				}

				//Optimiere die Komponente
				var optimization = component.Optimize(formatter);
				if (!optimization.IsEmpty)
					optimizations.Add((index, optimization));
			}

			//Das leere Wort am Ende wurde nicht angefügt
			if (mergeWord != null)
			{
				//Lösche das leere Wort später
				optimizations.Add((index, new SheetCompositeLineComponentOptimizationResult(mergeWord) { RemoveComponent = true }));
			}

			//Führe die Optimierungen (und Löschungen) aus
			ExecuteOptimizations(optimizations);

			//Fasse Komponenten zusammen
			optimizations.Clear();
			SheetCompositeLineComponent? componentBefore = default;
			index = -1;
			foreach (var component in owner.Components)
			{
				index++;

				//Zwei Wörter?
				if (componentBefore is SheetComplexWord wordBefore && component is SheetComplexWord word)
				{
					//Kann das Element mit dem vorherigen zusammengefasst werden?
					if (wordBefore.IsSpace == word.IsSpace)
					{
						//Füge die Komponenten zusammen und Optimiere
						wordBefore.Append(word);
						var optimization = wordBefore.Optimize(formatter);

						//Speichere ggf. die Optimierung
						if (!optimization.IsEmpty)
							optimizations.Add((index, optimization));

						//Entferne das zweite Wort
						optimizations.Add((index, new SheetCompositeLineComponentOptimizationResult(word) { RemoveComponent = true }));
					}
				}

				componentBefore = component;
			}

			//Führe noch einmal Optimierungen (und Löschungen) aus
			ExecuteOptimizations(optimizations);
		}

		private void ExecuteOptimizations(IEnumerable<(int index, SheetCompositeLineComponentOptimizationResult Optimization)> optimizations)
		{
			//Führe die Optimierungen (und Löschungen) aus
			var indexAdjustment = 0;
			foreach ((var index, var optimization) in optimizations)
			{
				var currentIndex = index + indexAdjustment;
#if DEBUG
				//currentIndex muss dem Index der Komponente entsprechen
				System.Diagnostics.Debug.Assert(currentIndex == owner.Components.IndexOf(optimization.Component));
#endif

				//Füge neue Elemente ein
				var replaced = false;
				foreach (var newComponent in optimization.NewComponentsAfter)
				{
					//Falls das Element entfernt werden soll, ersetze es einfach
					if (optimization.RemoveComponent && !replaced)
					{
						owner.Components[currentIndex] = newComponent;
						replaced = true;
					}
					else
					{
						//Füge das Element ein
						owner.Components.Insert(currentIndex++, newComponent);
						indexAdjustment++;
					}
				}

				//Entferne ggf. das Element
				if (!replaced)
				{
					owner.Components.RemoveAt(currentIndex);
					indexAdjustment--;
				}
			}
		}

		public LineEditResult InsertContent(string content, SimpleRange selectionRange, ISheetFormatter? formatter)
		{
			//Kürze ggf. Inhalt
			DeleteContentResult? deleteResult = null;
			if (selectionRange.Length > 0)
			{
				deleteResult = DeleteContentWithoutOptimization(selectionRange, formatter);
				if (deleteResult == null)
					return new LineEditResult(false, selectionRange);
			}

			//Finde die Elemente in, direkt links und direkt rechts von der Selection
			var elements = line.GetElementsIn(new SimpleRange(selectionRange.Start - 1, selectionRange.End + 1), formatter).ToList();

			//Liegt die Position mitten in einem Element?
			var insertOffset = selectionRange.Start;
			var fullyInsideElement = elements.Find(e => e.Offset < selectionRange.Start && e.Offset + e.Length > selectionRange.End);
			if (fullyInsideElement.Element != null)
			{
				//Muss Word sein
				if (fullyInsideElement.Element.Source is not SheetCompositeLineComponent sourceComponent || sourceComponent is not SheetComplexWord word)
				{
					return new LineEditResult(false, selectionRange);
				}

				//Füge den Content in das Element ein
				var wordOffset = selectionRange.Start - fullyInsideElement.Offset;
				word.Insert(wordOffset, content);
			}
			else
			{
				//Finde ggf. ein Element vor der Selection
				var leftOutsideElement = elements.FirstOrDefault(e => e.Offset + e.Length < selectionRange.Start);

				//Finde ggf. ein Element, das mit der Selection beginnt
				var leftInsideElement = elements.FirstOrDefault(e => e.Offset == selectionRange.Start);

				//Finde ggf. ein Element, das mit der Selection endet
				var rightInsideElement = elements.FirstOrDefault(e => e.Offset + e.Length == selectionRange.End);

				//Finde ggf. ein Element nach der Selection
				var rightOutsideElement = elements.FirstOrDefault(e => e.Offset > selectionRange.End);

				//Falls alle 4 gesetzt sind, ignoriere rightInsideElement
				if (leftOutsideElement.Element != null && leftInsideElement.Element != null && rightInsideElement.Element != null && rightOutsideElement.Element != null)
					rightInsideElement = default;

				//Jetzt gibt es maximal 3 relevante Elemente
				var relevantElements = new[] { leftInsideElement, leftOutsideElement, rightInsideElement, rightOutsideElement }.Where(e => e.Element != null).ToList();

				//Gibt es ein Element vor der Selection?
				var index = leftOutsideElement.Element?.Source is SheetCompositeLineComponent component ? owner.Components.IndexOf(component) : -1;
				SheetCompositeLineComponent newContent;
				if (index >= 0)
				{
					//Füge den neuen Content danach ein
					owner.Components.Insert(index + 1, newContent = CreateElementFromContent(content));
				}
				else
				{
					//Füge ggf. Leerzeichen ein
					var lastElementOffset = line.GetElementsIn(null, formatter).LastOrDefault().Offset;
					var spaceLength = selectionRange.Start - lastElementOffset;
					if (spaceLength > 0)
						owner.Components.Add(SheetComplexWord.CreateSpace(spaceLength));

					//Füge den neuen Content am Ende ein
					owner.Components.Add(newContent = CreateElementFromContent(content));
				}
			}

			//Optimiere die Zeile
			OptimizeRow(formatter, deleteResult?.ModifiedElements, newContent);

			return new LineEditResult(true, new SimpleRange(selectionRange.Start, selectionRange.Start));
		}

		private SheetCompositeLineComponent CreateElementFromContent(string content)
			=> new SheetComplexWord(new WordComponent(content));

		public LineEditResult DeleteContent(SimpleRange selectionRange, ISheetFormatter? formatter, bool forward = false)
		{
			//Behandle leere Selection wie eine Selection mit Länge 1
			if (selectionRange.Length == 0)
				selectionRange = forward ? new SimpleRange(selectionRange.Start, selectionRange.Start + 1)
					: new SimpleRange(selectionRange.Start, selectionRange.Start - 1);

			//Kürze den Inhalt
			var deleteResult = DeleteContentWithoutOptimization(selectionRange, formatter);

			//Fehler?
			if (deleteResult == null)
				return new LineEditResult(false, selectionRange);

			//Optimiere die Zeile
			OptimizeRow(formatter, deleteResult.ModifiedElements);
			return new LineEditResult(true, new SimpleRange(selectionRange.Start, selectionRange.Start));
		}

		private DeleteContentResult? DeleteContentWithoutOptimization(SimpleRange selectionRange, ISheetFormatter? formatter)
		{
			if (line is SheetDisplayTextLine)
			{
				//Finde alle Elemente, die in der Selection liegen
				var selectedElements = line.GetElementsIn(selectionRange, formatter).ToList();
				var modified = new List<SheetCompositeLineComponent>();
				foreach ((var elementOffset, var elementLength, var element) in selectedElements)
				{
					//Liegt das Element komplett in der Selection?
					var overlap = selectionRange.CheckOverlap(elementOffset, elementLength);
					if (overlap == RangeOverlap.InsideRange)
					{
						//Lösche das Element
						if (element.Source is WordComponent wordComponent)
						{
							//Lösche den Inhalt der Komponente, sie wird später wegoptimiert
							wordComponent.Text = string.Empty;
							if (wordComponent.Parent != null)
								modified.Add(wordComponent.Parent);
						}
						else
						{
							throw new NotSupportedException("Unbekannte Quelle " + element.Source?.GetType());
						}

						continue;
					}

					//Überschneidet sich das Element mit der Selection?
					if ((overlap | RangeOverlap.OverlapFlag) != 0)
					{
						//Berechne Länge und Offset der Kürzung
						var cutStartOffset = Math.Max(0, selectionRange.Start - elementOffset);
						var cutEndOffset = Math.Min(elementLength, selectionRange.End - elementOffset);
						var cutRange = new SimpleRange(cutStartOffset, cutEndOffset);

						if (cutRange.Length > 0)
						{
							//Kürze das Element
							if (element.Source is WordComponent wordComponent)
							{
								wordComponent.CutContent(cutRange, formatter);
								if (wordComponent.Parent != null)
									modified.Add(wordComponent.Parent);
							}
							else
							{
								throw new NotSupportedException("Unbekannte Quelle " + element.Source?.GetType());
							}
						}
					}
				}

				return new DeleteContentResult()
				{
					ModifiedElements = modified,
				};
			}

			return null;
		}

		private record DeleteContentResult
		{
			public IReadOnlyCollection<SheetCompositeLineComponent> ModifiedElements { get; init; } = Array.Empty<SheetCompositeLineComponent>();
		}
	}
}

public abstract class SheetCompositeLineComponent : SheetLineComponent
{
	public abstract IEnumerable<SheetCompositeLineBlock> CreateBlocks();

	internal abstract SheetCompositeLineComponentOptimizationResult Optimize(ISheetFormatter? formatter);
}

public sealed class SheetChord : SheetCompositeLineComponent, ISheetDisplayLineElementSource
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
					new SheetDisplayLineAnchorText(this, Suffix)));
		}
	}

	internal override SheetCompositeLineComponentOptimizationResult Optimize(ISheetFormatter? formatter)
		=> new SheetCompositeLineComponentOptimizationResult(this);
}

public sealed class SheetComplexWord : SheetCompositeLineComponent
{
	public bool IsEmpty => Components.All(c => c.IsEmpty);
	public bool IsSpace => Components.All(c => c.IsSpace);

	public ModifiableObservableCollectionWithParent<WordComponent, SheetComplexWord> Components { get; }

	public SheetComplexWord()
	{
		Components = Register(new ModifiableObservableCollectionWithParent<WordComponent, SheetComplexWord>(this));
	}

	public SheetComplexWord(params WordComponent[] components) : this((IEnumerable<WordComponent>)components) { }
	public SheetComplexWord(IEnumerable<WordComponent> components)
	{
		Components = Register(new ModifiableObservableCollectionWithParent<WordComponent, SheetComplexWord>(this, components));
	}

	public static SheetComplexWord CreateSpace(int length)
		=> new SheetComplexWord(new WordComponent(string.Empty.PadRight(length)));

	public void CheckFeatures(out bool zeroLength, out bool hasAttachments, out bool isSpace)
	{
		zeroLength = true;
		hasAttachments = false;
		isSpace = true;

		foreach (var component in Components)
		{
			if (!component.IsSpace)
			{
				isSpace = false;
				zeroLength = false;
			}
			else if (component.Text.Length != 0)
			{
				zeroLength = false;
			}

			if (component.Attachments.Count != 0)
			{
				hasAttachments = true;
			}
		}
	}

	public override IEnumerable<SheetCompositeLineBlock> CreateBlocks()
	{
		foreach (var component in Components)
		{
			var attachmentRows = component.Attachments.Select(a => a.CreateDisplayBlockLine()).ToList();

			SheetDisplayLineElement textElement = component.IsSpace && attachmentRows.Count == 0
				? new SheetDisplayLineSpace(component, component.Length)
				: new SheetDisplayLineAnchorText(component, component.Text)
				{
					Targets = attachmentRows.Select(b => b.Element).ToList()
				};
			var textRow = new SheetCompositeLineBlockRow<SheetDisplayTextLine.Builder>(textElement);

			yield return new SheetCompositeLineBlock(attachmentRows.Prepend(textRow));
		}
	}

	public void Prepend(SheetComplexWord word)
	{
		//Füge die Komponenten hinzu
		var i = 0;
		foreach (var component in word.Components)
			Components.Insert(i++, component);
	}

	public void Append(SheetComplexWord word)
	{
		//Füge die Komponenten hinzu
		foreach (var component in word.Components)
			Components.Add(component);
	}

	internal override SheetCompositeLineComponentOptimizationResult Optimize(ISheetFormatter? formatter)
	{
		//Sammle Attachments, deren Komponente leer ist
		IReadOnlyCollection<WordComponentAttachment>? freeAttachments = null;
		int attachmentOffset = 0;
		var currentOffset = 0;
		var deleteComponents = new List<WordComponent>();
		foreach (var component in Components)
		{
			var nextOffset = currentOffset + component.Text.Length;

			//Ist die Komponente leer?
			if (nextOffset == currentOffset)
			{
				//Speichere ggf. ihre Attachments
				if (component.Attachments.Count > 0)
				{
					freeAttachments = component.Attachments;
					attachmentOffset = currentOffset;
				}

				//Lösche die Komponente beim nächsten Durchgang
				deleteComponents.Add(component);
			}
			else
			{
				//Liegen die freien Attachments innerhalb der Komponente?
				if (freeAttachments != null && attachmentOffset >= currentOffset && attachmentOffset < nextOffset)
				{
					//Füge die Attachments in die Komponente ein
					foreach (var attachment in freeAttachments)
						component.Attachments.Add(attachment);
					freeAttachments = null;
				}
			}
		}

		//Lösche leere Komponenten
		foreach (var component in deleteComponents)
			Components.Remove(component);

		//Trenne das Wort an Leerzeichen und füge Komponenten zusammen
		deleteComponents.Clear();
		var enumerator = Components.GetEnumerator();
		if (enumerator.MoveNext())
		{
			//Erste Komponente
			var firstComponent = enumerator.Current;

			//Füge Komponenten zusammen
			bool hasNext;
			while (hasNext = enumerator.MoveNext())
			{
				//Können die Komponenten zusammengefügt werden?
				if (CanCombine(firstComponent, enumerator.Current))
				{
					//Füge die Komponenten zusammen
					firstComponent.Text += enumerator.Current.Text;

					//Entferne die zweite Komponente
					deleteComponents.Add(enumerator.Current);
				}
				else if (enumerator.Current.IsSpace == firstComponent.IsSpace)
				{
					//Lass die Komponenten getrennt
				}
				else
				{
					//Beginne ein neues Wort
					break;
				}
			}

			//Erzeuge neue Wörter aus den restlichen Komponenten
			if (hasNext)
			{
				//Erzeuge erstes neues Wort
				var newWords = new List<SheetComplexWord>();
				var currentComponent = enumerator.Current;
				var currentNewWord = new SheetComplexWord(currentComponent);
				newWords.Add(currentNewWord);

				//Gehe durch die restlichen Komponenten
				while (enumerator.MoveNext())
				{
					//Können die Komponenten zusammengefügt werden?
					if (CanCombine(currentComponent, enumerator.Current))
					{
						//Füge die Komponenten zusammen
						currentComponent.Text += enumerator.Current.Text;

						//Ignoriere die zweite Komponente
					}
					else if (enumerator.Current.IsSpace == currentComponent.IsSpace)
					{
						//Füge die Komponente dem neuen Wort hinzu
						currentNewWord.Components.Add(enumerator.Current);
					}
					else
					{
						//Beginne ein neues Wort
						currentNewWord = new SheetComplexWord(enumerator.Current);
						newWords.Add(currentNewWord);
					}
				}

				//Entferne ggf. Komponenten
				foreach (var component in deleteComponents)
					Components.Remove(component);

				return new SheetCompositeLineComponentOptimizationResult(this)
				{
					NewComponentsAfter = newWords,
				};
			}
		}

		//Entferne ggf. Komponenten
		foreach (var component in deleteComponents)
			Components.Remove(component);

		return new SheetCompositeLineComponentOptimizationResult(this);
	}

	private bool CanCombine(WordComponent c1, WordComponent c2)
	{
		if (c1.IsSpace != c2.IsSpace)
			return false;

		if (c2.Attachments.Count != 0)
			return false;

		return true;
	}

	public override string ToString() => string.Join(string.Empty, Components.Select(c => c.Text));

	//public override SheetLineComponentCutResult CutContent(SimpleRange range, ISheetFormatter? formatter)
	//{
	//	var cutOffset = range.Start;
	//	var cutLength = range.Length;

	//	foreach (var component in Components)
	//	{
	//		if (range.Length <= 0) break;

	//		//Ist der Offset noch nicht erreicht?
	//		if (cutOffset > 0)
	//		{
	//			//Ist die Komponente kürzer als Offset?
	//			if (component.Length <= cutOffset)
	//			{
	//				//Überspringe die Komponente
	//				cutOffset -= component.Length;
	//				continue;
	//			}

	//			//Fällt die Komponente komplett in die Kürzung?
	//			if (component.Length <= cutOffset + cutLength)
	//			{
	//				//Kürze die Komponente von rechts
	//				component.Text = component.Text[0..cutOffset];
	//				cutLength -= component.Length - cutOffset;
	//				cutOffset = 0;
	//				continue;
	//			}
	//			else
	//			{
	//				//Schneide einen Teil aus der Komponente heraus
	//				component.Text = component.Text[0..cutOffset] + component.Text[(cutOffset + cutLength)..];
	//				break;
	//			}
	//		}

	//		//Ist die Komponente länger?
	//		if (component.Length > cutLength)
	//		{
	//			//Kürze die Komponente
	//			component.Text = component.Text[cutLength..];
	//			break;
	//		}

	//		//Kürze die Komponente von links
	//		component.Text = component.Text[cutOffset..];
	//		cutLength -= cutOffset;
	//		cutOffset = 0;
	//		continue;
	//	}

	//	return new SheetLineComponentCutResult(true);
	//}
}

internal record SheetCompositeLineComponentOptimizationResult(SheetCompositeLineComponent Component)
{
	public bool IsEmpty => !RemoveComponent && NewComponentsAfter.Count == 0;

	public bool RemoveComponent { get; init; }
	public IReadOnlyCollection<SheetCompositeLineComponent> NewComponentsAfter { get; init; } = Array.Empty<SheetCompositeLineComponent>();
}