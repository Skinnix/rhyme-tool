//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Skinnix.RhymeTool.Data.Notation.Display;

//public record SheetCompositeLineBlock
//{
//    public IReadOnlyCollection<SheetCompositeLineBlockRow> Lines { get; }

//    public SheetCompositeLineBlock(params SheetCompositeLineBlockRow[] lineBlocks) : this((IEnumerable<SheetCompositeLineBlockRow>)lineBlocks) { }
//    public SheetCompositeLineBlock(IEnumerable<SheetCompositeLineBlockRow> lineBlocks)
//    {
//        Lines = lineBlocks.ToList().AsReadOnly();
//    }
//}

//public abstract record SheetCompositeLineBlockRow(SheetDisplayLineElement Element)
//{
//    public abstract bool CanAppend(SheetDisplayLineBuilderBase line);

//    public abstract SheetDisplayLineBuilderBase CreateBuilder();
//    public virtual SheetDisplayLineBuilderBase CreateBuilderAndAppend(int offset, ISheetFormatter? formatter = null)
//    {
//        var builder = CreateBuilder();
//        builder.ExtendLength(offset, 0);
//        builder.Append(Element, formatter);
//        return builder;
//    }
//}

//public sealed record SheetCompositeLineBlockRow<TLineBuilder>(SheetDisplayLineElement Element) : SheetCompositeLineBlockRow(Element)
//    where TLineBuilder : SheetDisplayLineBuilderBase, new()
//{
//    public override TLineBuilder CreateBuilder()
//        => new TLineBuilder();

//    public override bool CanAppend(SheetDisplayLineBuilderBase line)
//        => line is TLineBuilder;
//}