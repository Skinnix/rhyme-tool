namespace Skinnix.RhymeTool.Data.Notation.IO;

[Serializable]
public class SheetReaderException : Exception
{
	public SheetReaderException() { }

	public SheetReaderException(string? message)
		: base(message)
	{ }

	public SheetReaderException(string? message, Exception? innerException)
		: base(message, innerException)
	{ }
}