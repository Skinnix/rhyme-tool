namespace Skinnix.RhymeTool.Data.Notation.Display;

public interface ISheetDisplayLineEditing
{
	public bool InsertContent(string content, int selectionStart, int selectionEnd, ISheetFormatter? formatter);
	public bool DeleteContent(int selectionStart, int selectionEnd, ISheetFormatter? formatter, bool forward = false);
}