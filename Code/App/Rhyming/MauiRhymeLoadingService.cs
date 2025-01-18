using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.MauiBlazor.Rhyming;

public class MauiRhymeLoadingService : RhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Dictionaries/DAWB_words2.txt";

	protected override async Task<StreamReader> CreateWordDataReaderAsync()
		=> new StreamReader(await FileSystem.OpenAppPackageFileAsync(RHYME_DATA_PATH));
}
