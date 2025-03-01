﻿using System.Collections;
using System.Linq.Expressions;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Editing;
using Skinnix.RhymeTool.Data.Notation.Features;

namespace Skinnix.RhymeTool.Data.Notation;

public class SheetLineCollection : IReadOnlyList<SheetLine>, IModifiable
{
	public event EventHandler<ModifiedEventArgs>? Modified;
	public event EventHandler? TitlesChanged;

	private readonly SheetDocument document;
	private List<SheetLine> lines;

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
		this.lines = new(lines.Select(RegisterLine));
	}

	private SheetLine RegisterLine(SheetLine line)
	{
		if (line is ISheetTitleLine titleLine)
			titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
		return line;
	}

	private SheetLine DeregisterLine(SheetLine line)
	{
		if (line is ISheetTitleLine titleLine)
			titleLine.IsTitleLineChanged -= OnIsTitleLineChanged;
		return line;
	}

	private void OnIsTitleLineChanged(object? sender, EventArgs e)
		=> RaiseTitlesChanged();

	private void RaiseTitlesChanged()
		=> TitlesChanged?.Invoke(this, EventArgs.Empty);

	public IEnumerable<SheetLineContext> GetLinesWithContext()
	{
		var currentFeatures = document.GlobalFeatures.ToArray();
		SheetLineContext? previous = null;
		foreach (var line in lines)
		{
			if (line is ISheetFeatureLine featureLine)
			{
				var features = featureLine.GetFeatures();
				List<IDocumentFeature>? newFeatures = null;
				foreach (var feature in features)
				{
					newFeatures ??= new(currentFeatures);
					newFeatures.RemoveAll(feature.Overrides);
					newFeatures.Add(feature);
				}
				if (newFeatures is not null)
					currentFeatures = newFeatures.ToArray();
			}

			previous = new SheetLineContext(document, line, previous, currentFeatures);
			yield return previous;
		}
	}

	public SheetLineContext? GetContextFor(SheetLine line)
		=> GetLinesWithContext().FirstOrDefault(l => l.Line == line);

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
		if (line is ISheetTitleLine titleLine)
			titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void AddRange(IEnumerable<SheetLine> lines)
	{
		this.lines.AddRange(lines.Select(l =>
		{
			if (l is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
			return l;
		}));
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void Insert(int index, SheetLine line)
	{
		lines.Insert(index, line);
		if (line is ISheetTitleLine titleLine)
			titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void InsertRange(int index, IEnumerable<SheetLine> lines)
	{
		this.lines.InsertRange(index, lines.Select(l =>
		{
			if (l is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
			return l;
		}));
		RaiseModified(new ModifiedEventArgs(this));
	}

	public void Remove(SheetLine line)
	{
		lines.Remove(line);
		if (line is ISheetTitleLine titleLine)
			titleLine.IsTitleLineChanged -= OnIsTitleLineChanged;
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
			var current = lines[index];
			if (current is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged -= OnIsTitleLineChanged;

			var next = lines[index] = insertBefore.Count == 1 ? insertBefore.First() : insertAfter.First();
			if (next is ISheetTitleLine nextTitleLine)
				nextTitleLine.IsTitleLineChanged += OnIsTitleLineChanged;

			RaiseModified(new ModifiedEventArgs(this));
			return;
		}

		//Entferne ggf. Zeile dahinter
		if (removeLineAfter)
		{
			if (lines[index + 1] is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged -= OnIsTitleLineChanged;
			lines.RemoveAt(index + 1);
		}

		//Zeilen dahinter
		var countBefore = lines.Count;
		if (index == lines.Count - 1)
			lines.AddRange(insertAfter.Select(l =>
			{
				if (l is ISheetTitleLine titleLine)
					titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
				return l;
			}));
		else
			lines.InsertRange(index + 1, insertAfter.Select(l =>
			{
				if (l is ISheetTitleLine titleLine)
					titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
				return l;
			}));
		
		//Zeilen davor
		lines.InsertRange(index, insertBefore.Select(l =>
		{
			if (l is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged += OnIsTitleLineChanged;
			return l;
		}));

		//Entferne ggf. Zeile davor
		if (removeLineBefore)
		{
			if (lines[index - 1] is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged -= OnIsTitleLineChanged;
			lines.RemoveAt(index - 1);
		}

		//Entferne ggf. Zeile
		if (removeLine)
		{
			if (line is ISheetTitleLine titleLine)
				titleLine.IsTitleLineChanged -= OnIsTitleLineChanged;
			lines.Remove(line);
		}

		//Hat sich etwas verändert?
		if (removeLine || removeLineBefore || removeLineAfter || countBefore != lines.Count)
			RaiseModified(new ModifiedEventArgs(this));
	}

	public bool Replace(SheetLine existing, SheetLine line)
	{
		var index = lines.IndexOf(existing);
		if (index < 0)
			return false;

		lines[index] = line;
		RaiseModified(new ModifiedEventArgs(this));
		return true;
	}

	private void RaiseModified(ModifiedEventArgs args) => Modified?.Invoke(this, args);
	#endregion

	public Stored Store() => new(this);

	public void Restore(Stored store)
	{
		lines.Clear();
		lines.AddRange(store.Restore(document));
	}

	public readonly record struct Stored : IStored<SheetLineCollection, SheetDocument>
	{
		private readonly SheetLine.Stored[]? lines;

		internal Stored(SheetLineCollection collection)
		{
			if (collection.Count == 0)
			{
				lines = null;
			}
			else
			{
				lines = new SheetLine.Stored[collection.Count];
				foreach ((var i, var line) in collection.Index())
					lines[i] = line.Store();

				lines = ArrayCache.Cache(lines);
			}
		}

		private Stored(SheetLine.Stored[] lines)
		{
			this.lines = lines;
		}

		public SheetLineCollection Restore(SheetDocument owner)
			=> new(owner, lines?.Select(l => l.Restore()) ?? []);

		internal void Apply(SheetLineCollection target)
		{
			foreach (var line in target.lines)
				target.DeregisterLine(line);
			target.lines.Clear();

			if (lines is not null)
			{
				target.lines = new(lines.Length);
				foreach (var line in lines)
					target.lines.Add(target.RegisterLine(line.Restore()));
			}

			target.RaiseModified(new ModifiedEventArgs(target));
			target.RaiseTitlesChanged();
		}

		/*public Stored OptimizeWith(Stored collection)
		{
			var newLines = new SheetLine.Stored[lines.Length];
			for (var i = 0; i < newLines.Length; i++)
				newLines[i] = lines[i].OptimizeWith(collection.lines);

			if (newLines.Length == collection.lines.Length && newLines.SequenceEqual(collection.lines))
				return collection;

			return new(newLines);
		}*/
	}
}