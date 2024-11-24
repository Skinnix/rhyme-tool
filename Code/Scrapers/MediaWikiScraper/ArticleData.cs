using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MediaWikiScraper;

[XmlRoot("page", Namespace = "http://www.mediawiki.org/xml/export-0.11/")]
public class ArticleData
{
	[XmlElement("id")]
	public int Id { get; set; }

	[XmlElement("title")]
	public string? Title { get; set; }

	[XmlElement("revision")]
	public RevisionElement? Revision { get; set; }

	public IEnumerable<Section> EnumerateSections()
	{
		if (Revision?.Text?.Value is null)
			yield break;

		using (var stringReader = new StringReader(Revision.Text))
		using (var reader = new BufferReader(stringReader))
		{
			string? line;
			while ((line = reader.ReadLine()) is not null)
			{
				if (line.StartsWith("{{"))
					if (line.EndsWith("}}"))
						yield return new Section(line[2..^2], reader);
					else
						yield return new Section(line[2..], reader)
						{
							Closed = false,
						};
			}
		}
	}

	public class RevisionElement
	{
		[XmlElement("text")]
		public TextElement? Text { get; set; }
	}

	public class TextElement
	{
		[XmlText]
		public string? Value { get; set; }

		[return: NotNullIfNotNull(nameof(text))]
		public static implicit operator string?(TextElement? text) => text?.Value;
	}

	public class BufferReader(StringReader reader) : IDisposable
	{
		private string? buffer;

		public string? ReadLine()
		{
			if (buffer is not null)
			{
				var line = buffer;
				buffer = null;
				return line;
			}

			return reader.ReadLine();
		}

		public void BufferLine(string buffer)
		{
			this.buffer = buffer;
		}

		public void Dispose()
		{
			reader.Dispose();
			buffer = null;
		}
	}

	public class Section(string title, BufferReader reader) : IDisposable
	{
		public string Title { get; } = title;

		public bool Closed { get; init; } = true;

		public IEnumerable<string> ReadLines()
		{
			string? line;
			while ((line = reader.ReadLine()) is not null)
			{
				if (line.StartsWith("{{"))
				{
					reader.BufferLine(line);
					yield break;
				}

				yield return line;
			}
		}

		public void Dispose()
		{
			reader = null;
		}
	}
}
