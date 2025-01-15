using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Notation.Display;
using Skinnix.RhymeTool.Data.Notation.Features;

namespace Skinnix.RhymeTool.Data.Notation;

public abstract record SheetLineType(Type Type, string Label)
{
	public static SheetLineType Create<T>(string label) => new Simple(typeof(T), label);

	private sealed record Simple(Type Type, string Label) : SheetLineType(Type, Label);
}

public interface ISheetLine
{
	SheetLineType Type { get; }
	Guid Guid { get; }
	bool IsEmpty { get; }
}

public interface ISelectableSheetLine : ISheetLine
{
	static abstract SheetLineType LineType { get; }
}

public interface ISheetTitleLine : ISheetLine
{
	event EventHandler? IsTitleLineChanged;

	bool IsTitleLine(out string? title);
}

public interface ISheetFeatureLine : ISheetLine
{
	IEnumerable<IDocumentFeature> GetFeatures();
}

public abstract class SheetLine : DeepObservableBase, ISheetLine
{
	public static readonly Reason NoLineAfter = new("Keine Zeile danach");
	public static readonly Reason NoLineBefore = new("Keine Zeile davor");

	public SheetLineType Type { get; }

	public Guid Guid { get; set; } = Guid.NewGuid();

	public abstract bool IsEmpty { get; }

	protected SheetLine(SheetLineType type)
	{
		this.Type = type;
	}

    public abstract IEnumerable<SheetDisplayLine> CreateDisplayLines(SheetLineContext context, ISheetBuilderFormatter? formatter = null);

	public abstract IEnumerable<SheetLineConversion> GetPossibleConversions(ISheetBuilderFormatter? formatter = null);
}

public class SheetEmptyLine() : SheetLine(LineType), ISelectableSheetLine, ISheetDisplayLineEditing
{
	public static SheetLineType LineType { get; } = SheetLineType.Create<SheetEmptyLine>("Leer");

	SheetLine ISheetDisplayLineEditing.Line => this;
	public int LineId => 0;

	private int count = 1;
	public int Count
	{
		get => count;
		set => Set(ref count, value);
	}

	public override bool IsEmpty => true;

	public override IEnumerable<SheetLineConversion> GetPossibleConversions(ISheetBuilderFormatter? formatter = null)
		=> [SheetLineConversion.Simple<SheetTabLine>.Instance];

	public override IEnumerable<SheetDisplayLine> CreateDisplayLines(SheetLineContext context, ISheetBuilderFormatter? formatter = null)
    {
        for (int i = 0; i < Count; i++)
		{
			yield return new SheetDisplayEmptyLine(0)
			{
				Editing = this
			};
		}
    }

	public DelayedMetalineEditResult TryDeleteContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
		DeleteDirection direction, DeleteType type, ISheetEditorFormatter? formatter = null)
	{
		//Mehrzeilige Bearbeitung ignorieren, die Zeilen werden eh zusammengefasst
		if (multilineContext is not null)
			return new DelayedMetalineEditResult(() => new MetalineEditResult(new MetalineSelectionRange(this, SimpleRange.CursorAtStart)));

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
				return new MetalineEditResult(new MetalineSelectionRange(lineAfter, SimpleRange.CursorAtStart, MetalineSelectionRange.FIRST_LINE))
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
				return new MetalineEditResult(new MetalineSelectionRange(lineBefore, SimpleRange.CursorAtEnd, MetalineSelectionRange.LAST_LINE))
				{
					RemoveLine = true,
				};
			});
		}
	}

	public DelayedMetalineEditResult TryInsertContent(SheetDisplayLineEditingContext context, SheetDisplayMultiLineEditingContext? multilineContext,
		string content, ISheetEditorFormatter? formatter = null)
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
			EditRange = SimpleRange.CursorAtStart,
		};

		//Ersetze die Zeile mit einer VarietyLine und füge dann den Content ein
		var varietyLine = new SheetVarietyLine();
		var varietyLineResult = varietyLine.ContentEditor.InsertContent(context, multilineContext, content, formatter);

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