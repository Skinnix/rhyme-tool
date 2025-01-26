using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Web.Rhyming;

public class WebRhymeLoadingService(HttpClient http) : BinaryRhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words4.bin";

	protected override async Task<BinaryReader> GetRhymeReaderAsync()
		=> new BinaryReader(await http.GetStreamAsync(RHYME_DATA_PATH));
}

//public class WebRhymeLoadingService(HttpClient http) : CsvRhymeLoadingServiceBase
//{
//	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words3.txt";
//	private const string SPELLING_DIC_PATH = "Data/Dictionaries/de-DE.dic";
//	private const string SPELLING_AFF_PATH = "Data/Dictionaries/de-DE.aff";

//	protected override async Task<StreamReader> GetRhymeReaderAsync()
//		=> new StreamReader(await http.GetStreamAsync(RHYME_DATA_PATH));

//	protected override async Task<(Stream dictionary, Stream affix)> GetSpellingStreamsAsync()
//	{
//		var dicTask = http.GetStreamAsync(SPELLING_DIC_PATH);
//		var affTask = http.GetStreamAsync(SPELLING_AFF_PATH);
//		await Task.WhenAll(dicTask, affTask);
//		return (dicTask.Result, affTask.Result);
//	}
//}
