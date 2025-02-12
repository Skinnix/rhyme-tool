namespace Skinnix.RhymeTool.Client.IO;

[Serializable]
public class SheetWriterException : Exception
{
	public SheetWriterException() { }

	public SheetWriterException(string? message)
		: base(message)
	{ }

	public SheetWriterException(string? message, Exception? innerException)
		: base(message, innerException)
	{ }
}