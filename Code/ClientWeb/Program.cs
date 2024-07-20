using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Skinnix.RhymeTool;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Console.WriteLine(builder.HostEnvironment.BaseAddress);

builder.Services.AddRhymeToolClient();

#if DEBUG
builder.Services.AddScoped<HttpClient>(services => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IDebugDataService, WebDebugDataService>();
#endif

var host = builder.Build();

host.Services.UseRhymeToolClient();

#if DEBUG
var session = host.Services.GetRequiredService<WorkSession>();
var debugData = host.Services.GetRequiredService<IDebugDataService>();
await session.OpenDocument(await debugData.GetDebugFileAsync(), null);
#endif

await host.RunAsync();

class WebDebugDataService : IDebugDataService
{
	private readonly HttpClient httpClient;

	public WebDebugDataService(HttpClient httpClient)
	{
		this.httpClient = httpClient;
	}

	public Task<Stream> GetDebugFileAsync()
		=> httpClient.GetStreamAsync("Data/test-sas.txt");
}