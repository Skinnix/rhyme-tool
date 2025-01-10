using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvReader;

public abstract class WordFeature
{
	public abstract string? GetFeatureValue<TWord>(TWord word)
		where TWord : IRhymableWord;
}
