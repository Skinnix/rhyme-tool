using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Web.Rhyming;

#if false
public class ServerSideRhymeLoadingService(HttpClient http) : CsvRhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words3.txt";
	private const string RHYME_COMPARISON_DATA_PATH = "Data/Dictionaries/de-ipa.txt";
	private const string WORD_FORM_DATA_PATH = "Data/Dictionaries/wordforms.txt";
	private const string SPELLING_DIC_PATH = "Data/Dictionaries/de-DE.dic.txt";
	private const string SPELLING_AFF_PATH = "Data/Dictionaries/de-DE.aff.txt";

	protected override async Task<StreamReader> GetRhymeReaderAsync()
		=> new StreamReader(await http.GetStreamAsync(RHYME_DATA_PATH));

	protected override async Task<StreamReader> GetComparisonReaderAsync()
		=> new StreamReader(await http.GetStreamAsync(RHYME_COMPARISON_DATA_PATH));

	protected override async Task<StreamReader> GetWordFormReaderAsync()
		=> new StreamReader(await http.GetStreamAsync(WORD_FORM_DATA_PATH));

	//protected override async Task<(Stream dictionary, Stream affix)> GetSpellingStreamsAsync()
	//{
	//	var dicTask = await http.GetStreamAsync(SPELLING_DIC_PATH);
	//	var affTask = await http.GetStreamAsync(SPELLING_AFF_PATH);
	//	//await Task.WhenAll(dicTask, affTask);
	//	//return (dicTask.Result, affTask.Result);
	//	return (dicTask, affTask);

	//	//var root = @"D:\Users\Hendrik\Desktop\Visual Studio Projects\rhyme-tool\Code\ClientWeb\wwwroot\";
	//	//return (File.OpenRead(root + SPELLING_DIC_PATH), File.OpenRead(root + SPELLING_AFF_PATH));
	//}
}
#else
public class ServerSideRhymeLoadingService(HttpClient http) : RhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words4.bin";
	private const string RHYME_COMPARISON_DATA_PATH = "Data/Dictionaries/de-ipa.bin";
	private const string WORD_FORM_DATA_PATH = "Data/Dictionaries/wordforms.bin";
	private const string SPELLING_DIC_PATH = "Data/Dictionaries/de-DE.dic.txt";
	private const string SPELLING_AFF_PATH = "Data/Dictionaries/de-DE.aff.txt";

	protected override async Task<RhymeHelper> LoadInner()
	{
		RhymeWordList rhymeWordList;
		using (var stream = await http.GetStreamAsync(RHYME_DATA_PATH))
		using (var reader = new BinaryReader(stream))
		{
			rhymeWordList = RhymeWordList.Read(reader);
		}

		ComparisonWordList comparisonList;
		using (var stream = await http.GetStreamAsync(RHYME_COMPARISON_DATA_PATH))
		using (var reader = new BinaryReader(stream))
		{
			comparisonList = ComparisonWordList.Read(reader);
		}

		/*SpellingList spellingList;
		{
			var dicTask = http.GetStreamAsync(SPELLING_DIC_PATH);
			var affTask = http.GetStreamAsync(SPELLING_AFF_PATH);
			using (var dictionaryStream = await dicTask)
			using (var affixStream = await affTask)
			{
				spellingList = await SpellingList.LoadAsync(dictionaryStream, affixStream);
			}
		}*/

		WordFormList wordFormList;
		using (var stream = await http.GetStreamAsync(WORD_FORM_DATA_PATH))
		using (var reader = new BinaryReader(stream))
		{
			wordFormList = WordFormList.Read(reader);
		}

		return new(rhymeWordList, wordFormList, comparisonList);
	}
}
#endif

