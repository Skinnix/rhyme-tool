using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation.IO;

public class DefaultSheetEncoder(ISheetBuilderFormatter? formatter = null) : SheetEncoderBase(formatter ?? DefaultWriterFormatter.Instance)
{
	public DefaultSheetEncoder() : this(null) { }

	public override IEnumerable<string?> ProcessLines(SheetDocument document)
	{
		foreach (var lineContext in document.Lines.GetLinesWithContext())
		{
			var displayLines = lineContext.CreateDisplayLines(Formatter);
			foreach (var displayLine in displayLines)
			{
				foreach (var element in displayLine.GetElements())
				{
					var elementContent = element.ToString(Formatter) ?? string.Empty;
					yield return elementContent;
				}

				yield return null;
			}
		}
	}

	public record DefaultWriterFormatter : DefaultSheetFormatter
	{
		public static readonly new DefaultWriterFormatter Instance = new();

		public DefaultWriterFormatter()
		{
			CondenseTabNotes = false;
		}

		protected override string ToString(TabNote note, TabColumnWidth width, bool transform)
		{
			var result = base.ToString(note, width, transform);
			if (width.Min > 1)
				return " " + result + " ";

			return result;
		}
	}
}
