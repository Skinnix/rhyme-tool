using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Structure.Display;

public record SheetDisplayComponentBlock
{
	public IReadOnlyCollection<SheetDisplayComponentBlockLine> Lines { get; }

	public SheetDisplayComponentBlock(params SheetDisplayComponentBlockLine[] lineBlocks) : this((IEnumerable<SheetDisplayComponentBlockLine>)lineBlocks) { }
	public SheetDisplayComponentBlock(IEnumerable<SheetDisplayComponentBlockLine> lineBlocks)
	{
		Lines = lineBlocks.ToList().AsReadOnly();
	}
}

public abstract record SheetDisplayComponentBlockLine(SheetDisplayElement Element)
{
	public static SheetDisplayComponentBlockLine<TLineBuilder> Create<TLineBuilder>(SheetDisplayElement element)
		where TLineBuilder : SheetDisplayLineBuilder, new()
		=> new(element);

	public abstract bool CanAppend(SheetDisplayLineBuilder line);

	public abstract SheetDisplayLineBuilder CreateBuilder();
	public virtual SheetDisplayLineBuilder CreateBuilderAndAppend(int offset)
	{
		var builder = CreateBuilder();
		builder.ExtendLength(offset);
		builder.Append(Element);
		return builder;
	}
}

public sealed record SheetDisplayComponentBlockLine<TLineBuilder>(SheetDisplayElement Element) : SheetDisplayComponentBlockLine(Element)
	where TLineBuilder : SheetDisplayLineBuilder, new()
{
	public override TLineBuilder CreateBuilder()
		=> new TLineBuilder();

	public override string ToString()
		=> Element.ToString();

	public override bool CanAppend(SheetDisplayLineBuilder line)
		=> line is TLineBuilder;
}