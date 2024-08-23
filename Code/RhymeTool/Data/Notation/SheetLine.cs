using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Data.Notation;

public interface ISheetLine
{
	Guid Guid { get; }
}

public interface ISheetTitleLine : ISheetLine
{
	event EventHandler? IsTitleLineChanged;

	bool IsTitleLine(out string? title);
}

public abstract class SheetLine : DeepObservableBase, ISheetLine
{
	public static readonly Reason NoLineAfter = new("Keine Zeile danach");
	public static readonly Reason NoLineBefore = new("Keine Zeile davor");

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

	public DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter = null)
	{
		if (direction == DeleteDirection.Forward)
		{
			//Gibt es eine Zeile danach?
			var lineAfter = context.GetLineAfter?.Invoke();
			if (lineAfter is null)
				return DelayedMetalineEditResult.Fail(NoLineAfter);

			//Ist die Zeile auch leer?
			if (lineAfter is SheetEmptyLine)
			{
				//Lösche die nächste Zeile
				return new DelayedMetalineEditResult(() =>
				{
					return new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAtStart))
					{
						RemoveLineAfter = true,
					};
				});
			}

			//Lösche diese Zeile
			return new DelayedMetalineEditResult(() =>
			{
				return new MetalineEditResult(new MetalineSelectionRange(lineAfter, SimpleRange.CursorAtStart, 0))
				{
					RemoveLine = true,
				};
			});
		}
		else
		{
			//Gibt es eine Zeile davor?
			var lineBefore = context.GetLineBefore?.Invoke();
			if (lineBefore is null)
				return DelayedMetalineEditResult.Fail(NoLineBefore);

			//Lösche diese Zeile
			return new DelayedMetalineEditResult(() =>
			{
				return new MetalineEditResult(new MetalineSelectionRange(lineBefore, SimpleRange.CursorAtEnd, -1))
				{
					RemoveLine = true,
				};
			});
		}
	}

	public DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, string content, ISheetEditorFormatter? formatter = null)
	{
		//Wird nur ein Zeilenumbruch eingefügt?
		if (content == "\n")
		{
			//Erstelle eine neue leere Zeile
			var newLine = new SheetEmptyLine();
			return new DelayedMetalineEditResult(() =>
			{
				return new MetalineEditResult(new MetalineSelectionRange(newLine, SimpleRange.CursorAtStart))
				{
					InsertLinesAfter = [newLine]
				};
			});
		}

		//Update den Context, damit die Auswahl immer auf Null steht
		context = context with
		{
			SelectionRange = SimpleRange.CursorAtStart,
		};

		//Ersetze die Zeile mit einer VarietyLine und füge dann den Content ein
		var varietyLine = new SheetVarietyLine();
		var varietyLineResult = varietyLine.ContentEditor.InsertContent(context, content, formatter);

		//Nicht erfolgreich?
		if (!varietyLineResult.Success)
			return DelayedMetalineEditResult.Fail(varietyLineResult.FailReason);

		//Ersetze diese Zeile mit der neuen Zeile
		return new DelayedMetalineEditResult(() =>
		{
			return varietyLineResult with
			{
				RemoveLine = true,
				InsertLinesAfter = [varietyLine, ..varietyLineResult.InsertLinesAfter]
			};
		});
	}

	public ReasonBase? SupportsEdit(SheetDisplayMultiLineEditingContext context) => null;
}