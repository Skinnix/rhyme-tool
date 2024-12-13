using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScraperBase;

namespace DwdsScraper;

public class CompressedArticle
{
	public string Title { get; }
	public string Content { get; }

	public CompressedArticle(string title, string content)
	{
		Title = title;
		Content = content;
	}

	#region Read/Write
	public void WriteBinary(BinaryWriter writer)
	{
		writer.Write(Title);
		writer.Write(Content);
	}

	public static CompressedArticle ReadBinary(BinaryReader reader)
	{
		var title = reader.ReadString();
		var content = reader.ReadString();

		return new CompressedArticle(title, content);
	}
	#endregion
}
