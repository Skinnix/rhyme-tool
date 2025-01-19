using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Web.Rhyming;

public class ServerSideRhymeLoadingService(HttpClient http) : CsvRhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words3.txt";

	protected override async Task<StreamReader> GetReaderAsync()
		=> new StreamReader(await http.GetStreamAsync(RHYME_DATA_PATH));

	//protected override Task<RhymeHelper> CreateRhymeHelper(WordList wordList)
	//{
	//	var result = base.CreateRhymeHelper(wordList);

	//	var characters = new string(wordList.SelectMany(w => w.Ipa).Distinct().ToArray());

	//	using (var stream = File.OpenWrite(@"%userprofile%\Desktop\DAWB_words4.bin"))
	//	using (var writer = new BinaryWriter(stream))
	//	{
	//		wordList.Write(writer, (w, d) => writer.Write(d.Frequency));
	//	}

	//	return result;
	//}
}
