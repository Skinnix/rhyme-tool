namespace Skinnix.RhymeTool.Data.Notation.Display;

public record SheetDisplayTag(string Name)
{
	public static readonly SheetDisplayTag Attachment = new("attachment");
}
