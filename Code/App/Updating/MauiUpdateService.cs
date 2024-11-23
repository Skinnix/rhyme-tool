using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Skinnix.RhymeTool.Client.Updating;

namespace Skinnix.RhymeTool.MauiBlazor.Updating;

internal class MauiUpdateService(IOptions<UpdateOptions> options, HttpClient httpClient) : UpdateServiceBase(options, httpClient, false)
{
	protected override Task StartDownload(IUpdateService.IDownloadElement element, string url)
		=> Browser.OpenAsync(url);
}
