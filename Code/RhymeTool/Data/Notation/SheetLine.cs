﻿using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public interface ISheetLine
{
	Guid Guid { get; }
}

public interface ISheetTitleLine : ISheetLine
{
	bool IsTitleLine(out string? title);
}

public abstract class SheetLine : DeepObservableBase, ISheetLine
{
	public Guid Guid { get; set; } = Guid.NewGuid();

    public abstract IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null);
}

public class SheetEmptyLine : SheetLine, ISheetDisplayLineEditing
{
	SheetLine ISheetDisplayLineEditing.Line => this;
	public int LineId => 0;

	private int count = 1;
	public int Count
	{
		get => count;
		set => Set(ref count, value);
	}

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(ISheetBuilderFormatter? formatter = null)
    {
        for (int i = 0; i < Count; i++)
		{
			yield return new SheetDisplayEmptyLine(0)
			{
				Editing = this
			};
		}
    }

	public MetalineEditResult DeleteContent(SheetDisplayLineEditingContext context, bool forward = false, ISheetEditorFormatter? formatter = null)
	{
		if (forward)
		{
			//Gibt es eine Zeile danach?
			var lineAfter = context.GetLineAfter?.Invoke();
			if (lineAfter is null)
				return MetalineEditResult.Fail;

			//Ist die Zeile auch leer?
			if (lineAfter is SheetEmptyLine)
			{
				//Lösche die nächste Zeile
				return new MetalineEditResult(true, new MetalineSelectionRange(this, SimpleRange.Zero))
				{
					RemoveLineAfter = true,
				};
			}

			//Lösche diese Zeile
			return new MetalineEditResult(true, new MetalineSelectionRange(lineAfter, SimpleRange.Zero, 0))
			{
				RemoveLine = true,
			};
		}
		else
		{
			//Gibt es eine Zeile davor?
			var lineBefore = context.GetLineBefore?.Invoke();
			if (lineBefore is null)
				return MetalineEditResult.Fail;

			//Lösche diese Zeile
			return new MetalineEditResult(true, new MetalineSelectionRange(lineBefore, SimpleRange.Zero, -1))
			{
				RemoveLine = true,
			};
		}
	}

	public MetalineEditResult InsertContent(SheetDisplayLineEditingContext context, string content, ISheetEditorFormatter? formatter = null)
	{
		//Wird nur ein Zeilenumbruch eingefügt?
		if (content == "\n")
		{
			//Erstelle eine neue leere Zeile
			var newLine = new SheetEmptyLine();
			return new MetalineEditResult(true, new MetalineSelectionRange(newLine, SimpleRange.Zero))
			{
				InsertLinesAfter = [newLine]
			};
		}

		//Update den Context, damit die Auswahl immer auf Null steht
		context = context with
		{
			SelectionRange = SimpleRange.Zero,
		};

		//Ersetze die Zeile mit einer VarietyLine und füge dann den Content ein
		var varietyLine = new SheetVarietyLine();
		var varietyLineResult = varietyLine.ContentEditor.InsertContent(context, content, formatter);

		//Nicht erfolgreich?
		if (!varietyLineResult.Success)
			return MetalineEditResult.Fail;

		//Ersetze diese Zeile mit der neuen Zeile
		return varietyLineResult with
		{
			RemoveLine = true,
			InsertLinesAfter = [varietyLine, ..varietyLineResult.InsertLinesAfter]
		};
	}
}