using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HunspellSharp;

namespace Skinnix.RhymeTool.Rhyming;

public class SpellingList
{
	private readonly Hunspell hunspell;

	private SpellingList(Hunspell hunspell)
	{
		this.hunspell = hunspell;
	}

	public static SpellingList Load(Stream dictionaryStream, Stream affixStream)
		=> new SpellingList(new Hunspell(affixStream, dictionaryStream));

	public static async Task<SpellingList> LoadAsync(Stream dictionaryStream, Stream affixStream)
	{
		using (var dictionaryMs = new MemoryStream())
		using (var affixMs = new MemoryStream())
		{
			await dictionaryStream.CopyToAsync(dictionaryMs);
			await affixStream.CopyToAsync(affixMs);
			dictionaryMs.Position = 0;
			affixMs.Position = 0;
			return Load(dictionaryMs, affixMs);
		}
	}

	public List<string> TryGetStem(string word)
	{
		return hunspell.Stem(word);
		
	}

	public List<string> TryMorph(string word, string form)
	{
		return hunspell.Generate(word, form);
	}
}
