using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ScraperBase;

namespace ScraperBase;

[XmlRoot("word")]
public class WordInfo
{
	public const string HYPHENATION_SEPARATOR = "·";

	[JsonPropertyName("f")]
	[XmlArray("forms")]
	[XmlArrayItem("form")]
	public List<WordForm> Forms { get; set; } = new();

	[XmlIgnore, JsonIgnore]
	public WordForm? DefaultForm => Forms.FirstOrDefault();

	[JsonPropertyName("b")]
	[XmlAttribute("baseForm")]
	public string? BaseForm { get; set; }

	[XmlAttribute("popularity")]
	[JsonPropertyName("p")]
	public int Popularity { get; set; }

	[XmlAttribute("antiPopulatity")]
	[JsonPropertyName("a")]
	public int AntiPopularity { get; set; }

	[JsonConstructor]
	public WordInfo() { }

	public WordInfo(string word)
	{
		Forms.Add(new WordForm(word));
	}

	public WordForm AddForm(string text, string? label)
	{
		if (string.IsNullOrEmpty(text))
			throw new ArgumentException("Text darf nicht leer sein", nameof(text));

		foreach (var form in Forms)
		{
			if (form.Text == text)
			{
				if (label is not null && !form.Labels.Contains(label))
					form.Labels.Add(label);
				return form;
			}
		}

		var newForm = new WordForm(text);
		if (label is not null)
			newForm.Labels.Add(label);
		Forms.Add(newForm);
		return newForm;
	}

	public WordForm? AddHyphenation(string hyphenation, string? label)
	{
		var cleaned = hyphenation.Replace(HYPHENATION_SEPARATOR, null).Trim();
		if (cleaned == string.Empty)
			return null;

		var positions = new List<byte>();
		byte i = 0;
		foreach (var c in hyphenation)
		{
			if (c == HYPHENATION_SEPARATOR[0])
				positions.Add((byte)(i - positions.Count));
			i++;
		}
		if (positions.Count == 0 || positions[0] == 0 || positions[^1] >= cleaned.Length)
			return null;

		foreach (var form in Forms)
		{
			if (form.Text == cleaned)
			{
				if (!form.Hyphenations.Any(h => h.SequenceEqual(positions)))
					form.Hyphenations.Add(positions);

				if (label is not null && !form.Labels.Contains(label))
					form.Labels.Add(label);

				return form;
			}
		}

		var newForm = AddForm(cleaned, label);
		newForm.Hyphenations.Add(positions);
		return newForm;
	}

	public RhymeInfo AddRhyme(string rhyme, string? language)
	{
		if (DefaultForm is null)
			throw new InvalidOperationException("Cannot add rhyme to word without default form");

		foreach (var languageGroup in DefaultForm.Rhymes)
		{
			if (languageGroup.Language == language)
			{
				if (!languageGroup.Values.Contains(rhyme))
					languageGroup.Values.Add(rhyme);

				return languageGroup;
			}
		}

		var newGroup = new RhymeInfo { Language = language };
		newGroup.Values.Add(rhyme);
		DefaultForm.Rhymes.Add(newGroup);
		return newGroup;
	}

	public override string ToString() => DefaultForm?.Text ?? string.Empty;

	#region Read/Write
	public void WriteBinary(BinaryWriter writer)
	{
		writer.Write(BaseForm ?? string.Empty);
		writer.Write(Popularity);
		writer.Write(AntiPopularity);

		writer.WriteCollection(Forms, form =>
		{
			writer.Write(form.Text);

			writer.WriteCollection(form.Components, component =>
			{
				writer.Write(component);
			});

			writer.WriteCollection(form.Labels, label =>
			{
				writer.Write(label);
			});

			writer.WriteCollection(form.Hyphenations, hyphenation =>
			{
				writer.WriteCollection(hyphenation, position =>
				{
					writer.Write(position);
				});
			});

			writer.WriteCollection(form.Rhymes, rhyme =>
			{
				writer.Write(rhyme.Language ?? string.Empty);

				writer.WriteCollection(rhyme.Values, value =>
				{
					writer.Write(value);
				});
			});
		});
	}

	public static WordInfo ReadBinary(BinaryReader reader)
	{
		var baseForm = reader.ReadString();
		if (baseForm == string.Empty)
			baseForm = null;

		var popularity = reader.ReadInt32();
		var antiPopularity = reader.ReadInt32();

		var forms = reader.ReadCollection(() =>
		{
			var text = reader.ReadString();
			var components = reader.ReadCollection(reader.ReadString);

			var labels = reader.ReadCollection(reader.ReadString);
			var hyphenations = reader.ReadCollection(() =>
			{
				var positions = reader.ReadCollection(reader.ReadByte);
				return positions;
			});

			var rhymes = reader.ReadCollection(() =>
			{
				var language = reader.ReadString();
				if (language == string.Empty)
					language = null;

				var values = reader.ReadCollection(reader.ReadString);
				return new RhymeInfo
				{
					Language = language,
					Values = values
				};
			});

			return new WordForm
			{
				Text = text,
				Components = components,
				Labels = labels,
				Hyphenations = hyphenations,
				Rhymes = rhymes,
			};
		});

		return new WordInfo
		{
			BaseForm = baseForm,
			Forms = forms,
			Popularity = popularity,
			AntiPopularity = antiPopularity,
		};
	}
	#endregion

	public class WordForm
	{
		[XmlAttribute("text")]
		[JsonPropertyName("t"), JsonRequired]
		public string Text { get; set; } = string.Empty;

		[JsonPropertyName("l")]
		[XmlArray("labels")]
		public List<string> Labels { get; set; } = new();

		[JsonPropertyName("h")]
		[XmlArray("hyphenations")]
		[XmlArrayItem("hyphenation")]
		public List<List<byte>> Hyphenations { get; set; } = new();

		[JsonPropertyName("r")]
		[XmlArray("rhymes")]
		[XmlArrayItem("language")]
		public List<RhymeInfo> Rhymes { get; set; } = new();

		[JsonPropertyName("c")]
		[XmlElement("somponents")]
		public List<string> Components { get; set; } = new();

		[JsonConstructor]
		public WordForm() { }

		public WordForm(string text)
		{
			Text = text;
		}

		public override string ToString() => Text;
	}

	public class RhymeInfo
	{
		[JsonPropertyName("l")]
		[XmlAttribute("language")]
		public string? Language { get; set; }

		[JsonPropertyName("v")]
		[XmlArray("values")]
		[XmlArrayItem("rhyme")]
		public List<string> Values { get; set; } = new();

		public override string ToString() => Values.FirstOrDefault() ?? string.Empty;
	}
}
