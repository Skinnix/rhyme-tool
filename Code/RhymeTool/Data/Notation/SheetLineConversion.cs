namespace Skinnix.RhymeTool.Data.Notation;

public abstract class SheetLineConversion
{
	public SheetLineType Type { get; }

	public SheetLineConversion(SheetLineType type)
	{
		Type = type;
	}

	public abstract SheetLine Convert(SheetLine origin);

	public void Execute(SheetDocument document, SheetLine origin)
	{
		if (!document.Lines.Replace(origin, Convert(origin)))
			throw new InvalidOperationException("Ursprungszeile nicht gefunden");
	}

	public sealed class Simple<TLine> : SheetLineConversion
		where TLine : SheetLine, ISelectableSheetLine, new()
	{
		public static Simple<TLine> Instance { get; } = new();

		private Simple() : base(TLine.LineType) { }

		public override SheetLine Convert(SheetLine origin) => new TLine()
		{
			Guid = origin.Guid
		};
	}

	public sealed class Delegate : SheetLineConversion
	{
		private readonly Func<SheetLine, SheetLine> createLine;

		public Delegate(SheetLineType type, Func<SheetLine, SheetLine> createLine)
			: base(type)
		{
			this.createLine = createLine;
		}

		public override SheetLine Convert(SheetLine origin) => createLine(origin);
	}
}
