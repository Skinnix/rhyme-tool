using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Skinnix.RhymeTool.Updating;

namespace Skinnix.RhymeTool.MauiBlazor.Updating;

internal class MauiUpdateService(IOptions<UpdateOptions> options, HttpClient httpClient) : UpdateServiceBase(options, httpClient)
{
	protected override Task StartDownload(string url)
		=> Browser.OpenAsync(url);
}
