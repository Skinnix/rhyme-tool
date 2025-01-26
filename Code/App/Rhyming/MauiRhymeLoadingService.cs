using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.MauiBlazor.Rhyming;

public class MauiRhymeLoadingService : BinaryRhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Dictionaries/DAWB_words4.bin";

	protected override async Task<BinaryReader> GetRhymeReaderAsync()
		=> new BinaryReader(await FileSystem.OpenAppPackageFileAsync(RHYME_DATA_PATH));
}
