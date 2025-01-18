using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Web.Rhyming;

[DataContract]
public class ServerSideRhymeLoadingService(HttpClient http) : RhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words2.txt";

	protected override async Task<StreamReader> CreateWordDataReaderAsync()
		=> new StreamReader(await http.GetStreamAsync(RHYME_DATA_PATH));

	protected override Task<RhymeHelper> CreateRhymeHelper(WordList wordList)
	{
		var result = base.CreateRhymeHelper(wordList);

		using (var stream = File.OpenWrite(@"C:\Users\Hendrik\Desktop\words3.bin"))
		using (var writer = new BinaryWriter(stream))
		{
			wordList.Write(writer, (w, d) => writer.Write((byte)d.Frequency));
		}

		return result;
	}
}
