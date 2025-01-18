using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Rhyming;

public record struct IpaWord(string Word, string Ipa) : IRhymableWord, IWordIpa;

public interface IWordIpa : IWordDetail
{
	public string Ipa { get; }
}

public class IpaRhymeGroup(int maxSyllables = 3) : WordFeature
{
	public override string? GetFeatureValue<TWord>(TWord word)
	{
		var feature = word.TryGetDetail<IWordIpa>();
		var ipa = feature?.Ipa;
		if (string.IsNullOrEmpty(ipa))
			return null;

		return IpaHelper.GetRhymeSuffix(ipa, maxSyllables);
	}
}
