using System.Collections;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetLineCollection : IReadOnlyList<SheetLine>, IModifiable
{
	public event EventHandler<ModifiedEventArgs>? Modified;

	private readonly SheetDocument document;
	private readonly List<SheetLine> lines;

	public int Count => lines.Count;
	public SheetLine this[int index] => lines[index];

	public SheetLineCollection(SheetDocument document)
	{
		this.document = document;
		lines = new();
	}

	public SheetLineCollection(SheetDocument document, IEnumerable<SheetLine> lines)
	{
		this.document = document;
		this.lines = new(lines);
	}

	#region ReadOnlyCollection Members
	public int IndexOf(SheetLine line) => lines.IndexOf(line);

	public SheetLine? GetLineBefore(SheetLine line)
	{
		var lineIndex = lines.IndexOf(line);
		if (lineIndex <= 0)
			return null;

		return lines[lineIndex - 1];
	}

	public SheetLine? GetLineAfter(SheetLine line)
	{
		var lineIndex = lines.IndexOf(line);
		if (lineIndex < 0 || lineIndex >= lines.Count - 1)
			return null;

		return lines[lineIndex + 1];
	}

	public IEnumerator<SheetLine> GetEnumerator() => lines.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	#endregion

	#region Modify
	public void Add(SheetLine line)
	{
		lines.Add(line);
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void AddRange(IEnumerable<SheetLine> lines)
	{
		this.lines.AddRange(lines);
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void Insert(int index, SheetLine line)
	{
		lines.Insert(index, line);
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void InsertRange(int index, IEnumerable<SheetLine> lines)
	{
		this.lines.InsertRange(index, lines);
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void Remove(SheetLine line)
	{
		lines.Remove(line);
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void InsertAndRemove(SheetLine line, bool removeLine, bool removeLineBefore, bool removeLineAfter,
		IReadOnlyCollection<SheetLine> insertBefore, IReadOnlyCollection<SheetLine> insertAfter)
	{
		var index = lines.IndexOf(line);
		if (index == -1)
			throw new ArgumentException("Line not found");

		//Sonderfall: Zeile ersetzen
		if (removeLine && insertBefore.Count + insertAfter.Count == 1)
		{
			lines[index] = insertBefore.Count == 1 ? insertBefore.First() : insertAfter.First();
			RaiseModified(new ModifiedEventArgs(this));
			return;
		}

		//Entferne ggf. Zeile dahinter
		if (removeLineAfter)
			lines.RemoveAt(index + 1);

		//Zeilen dahinter
		var countBefore = lines.Count;
		if (index == lines.Count - 1)
			lines.AddRange(insertAfter);
		else
			lines.InsertRange(index + 1, insertAfter);
		
		//Zeilen davor
		lines.InsertRange(index, insertBefore);

		//Entferne ggf. Zeile davor
		if (removeLineBefore)
			lines.RemoveAt(index - 1);

		//Entferne ggf. Zeile
		if (removeLine)
			lines.Remove(line);

		//Hat sich etwas verändert?
		if (removeLine || removeLineBefore || removeLineAfter || countBefore != lines.Count)
			RaiseModified(new ModifiedEventArgs(this));
	}

	private void RaiseModified(ModifiedEventArgs args) => Modified?.Invoke(this, args);
	#endregion
}