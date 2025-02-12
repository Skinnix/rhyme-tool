using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;

namespace Skinnix.RhymeTool.Client.IO;

public class DefaultSheetEncoder(ISheetBuilderFormatter? formatter = null) : SheetEncoderBase<string>(formatter ?? DefaultWriterFormatter.Instance)
{
	public DefaultSheetEncoder() : this(null) { }

	public override IEnumerable<string?> ProcessLines(SheetDocument document)
	{
		foreach (var lineContext in document.Lines.GetLinesWithContext())
		{
			var displayLines = lineContext.CreateDisplayLines(Formatter);
			foreach (var displayLine in displayLines)
			{
				yield return string.Join(null, displayLine.GetElements().Select(e => e.ToString()));
				//foreach (var element in displayLine.GetElements())
				//	yield return element.ToString();

				//yield return null;
			}
		}
	}

	public record DefaultWriterFormatter : DefaultSheetFormatter
	{
		public static readonly new DefaultWriterFormatter Instance = new();

		public DefaultWriterFormatter()
		{
			CondenseTabNotes = false;
			MajorSeventhDegreeModifier = null;

			//var properties = GetType().GetProperties();
			//foreach (var property in properties)
			//{
			//	if (!property.Name.StartsWith("Text"))
			//		continue;

			//	var other = properties.FirstOrDefault(p => p.Name == property.Name[4..]);
			//	if (other is null)
			//		continue;

			//	if (property.PropertyType != other.PropertyType || !property.CanRead || !other.CanWrite)
			//		continue;

			//	var value = property.GetValue(this);
			//	other.SetValue(this, value);
			//}
		}

		protected override string ToString(TabNote note, TabColumnWidth width, bool transform)
		{
			var result = base.ToString(note, width, transform);
			if (width.Min > 1)
				return " " + result + " ";

			return result;
		}

		protected override TabNote.TabNoteFormat Format(TabNote note, string noteString, TabColumnWidth width)
		{
			var result = base.Format(note, noteString, width);
			if (!CondenseTabNotes && width.Min > 1)
				result = result with
				{
					Text = " " + noteString + " "
				};

			return result;
		}
	}
}
