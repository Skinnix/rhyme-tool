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

	public virtual int FirstExistingLineId => 0;

	protected SheetLine(SheetLineType type)
	{
		this.Type = type;
	}

	public abstract IEnumerable<SheetDisplayLine> CreateDisplayLines(SheetLineContext context, ISheetBuilderFormatter? formatter = null);

	public abstract IEnumerable<SheetLineConversion> GetPossibleConversions(ISheetBuilderFormatter? formatter = null);

	public abstract Stored Store();

	public abstract class Stored : IStored<SheetLine>
	{
		public abstract SheetLine Restore();

		//public abstract Stored OptimizeWith(IReadOnlyCollection<Stored> lines);
	}
}
