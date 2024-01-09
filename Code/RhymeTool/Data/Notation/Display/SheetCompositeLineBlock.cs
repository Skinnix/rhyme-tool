using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation.Display;

public record SheetCompositeLineBlock
{
    public IReadOnlyCollection<SheetCompositeLineBlockRow> Lines { get; }

    public SheetCompositeLineBlock(params SheetCompositeLineBlockRow[] lineBlocks) : this((IEnumerable<SheetCompositeLineBlockRow>)lineBlocks) { }
    public SheetCompositeLineBlock(IEnumerable<SheetCompositeLineBlockRow> lineBlocks)
    {
        Lines = lineBlocks.ToList().AsReadOnly();
    }
}

public abstract record SheetCompositeLineBlockRow(SheetDisplayLineElement Element)
{
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

public sealed record SheetCompositeLineBlockRow<TLineBuilder>(SheetDisplayLineElement Element) : SheetCompositeLineBlockRow(Element)
    where TLineBuilder : SheetDisplayLineBuilder, new()
{
    public override TLineBuilder CreateBuilder()
        => new TLineBuilder();

    public override bool CanAppend(SheetDisplayLineBuilder line)
        => line is TLineBuilder;
}