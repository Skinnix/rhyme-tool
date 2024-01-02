using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Structure.Display;

public record SheetCompositeLineBlock
{
	public IReadOnlyCollection<SheetCompositeLineBlockLine> Lines { get; }

	public SheetCompositeLineBlock(params SheetCompositeLineBlockLine[] lineBlocks) : this((IEnumerable<SheetCompositeLineBlockLine>)lineBlocks) { }
	public SheetCompositeLineBlock(IEnumerable<SheetCompositeLineBlockLine> lineBlocks)
	{
		Lines = lineBlocks.ToList().AsReadOnly();
	}
}

public abstract record SheetCompositeLineBlockLine(SheetDisplayElement Element)
{
	public static SheetDisplayComponentBlockLine<TLineBuilder> Create<TLineBuilder>(SheetDisplayElement element)
		where TLineBuilder : SheetDisplayLineBuilder, new()
		=> new(element);

	public abstract bool CanAppend(SheetDisplayLineBuilder line);

	public abstract SheetDisplayLineBuilder CreateBuilder();
	public virtual SheetDisplayLineBuilder CreateBuilderAndAppend(int offset, ISheetFormatter? formatter = null)
	{
		var builder = CreateBuilder();
		builder.ExtendLength(offset, 0);
		builder.Append(Element, formatter);
		return builder;
	}
}

public sealed record SheetDisplayComponentBlockLine<TLineBuilder>(SheetDisplayElement Element) : SheetCompositeLineBlockLine(Element)
	where TLineBuilder : SheetDisplayLineBuilder, new()
{
	public override TLineBuilder CreateBuilder()
		=> new TLineBuilder();

	public override bool CanAppend(SheetDisplayLineBuilder line)
		=> line is TLineBuilder;
}