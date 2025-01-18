using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
//using BlazorWorker.BackgroundServiceFactory;
//using BlazorWorker.Core;
//using BlazorWorker.Core.CoreInstanceService;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Rhyming;

namespace Skinnix.RhymeTool.Web.Rhyming;

[DataContract]
public class WebRhymeLoadingService(HttpClient http) : RhymeLoadingServiceBase
{
	private const string RHYME_DATA_PATH = "Data/Dictionaries/DAWB_words2.txt";

	//private static RhymeHelper? rhymeHelper;

	/*protected override async Task<RhymeHelper> LoadInner()
	{
		try
		{
			var worker = await workerFactory.CreateAsync();
			var service = await worker.CreateBackgroundServiceAsync<Worker>();
			var url = http.BaseAddress is null ? RHYME_DATA_PATH : new Uri(http.BaseAddress, RHYME_DATA_PATH).ToString();
			await service.RunAsync(instance => instance.LoadAsync(url));
			return rhymeHelper!;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex);
			throw;
		}
	}*/

	protected override async Task<StreamReader> CreateWordDataReaderAsync()
		=> new StreamReader(await http.GetStreamAsync(RHYME_DATA_PATH));

	/*[DataContract]
	private class Worker
	{
		public async Task LoadAsync(string url)
		{
			var loader = new Loader(url);
			var helper = await loader.LoadRhymeHelperAsync();
			WebRhymeLoadingService.rhymeHelper = helper;
		}

		private class Loader(string url) : RhymeLoadingServiceBase
		{
			protected override async Task<StreamReader> CreateWordDataReaderAsync()
			{
				using (var http = new HttpClient())
				{
					return new StreamReader(await http.GetStreamAsync(url));
				}
			}
		}
	}*/
}
