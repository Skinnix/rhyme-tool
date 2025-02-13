using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Skinnix.RhymeTool;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Client.Updating;
using Skinnix.RhymeTool.Web.Rhyming;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Console.WriteLine(builder.HostEnvironment.BaseAddress);

builder.Services.AddRhymeToolClient();

//HTTP-Client
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

//Reime
builder.Services.AddSingleton<IRhymeLoadingService, WebRhymeLoadingService>();

//Update-Service
builder.Services.AddScoped<IUpdateService, UpdateService>();

#if DEBUG
builder.Services.AddScoped<IDebugDataService, WebDebugDataService>();
#endif

var host = builder.Build();

host.Services.UseRhymeToolClient();

#if DEBUG
var documentService = host.Services.GetRequiredService<IDocumentService>();
var debugData = host.Services.GetRequiredService<IDebugDataService>();
var documentSource = await documentService.LoadFile(await debugData.GetDebugFileAsync());
documentService.SetCurrentDocument(documentSource);
#endif

await host.RunAsync();

#if DEBUG
public class WebDebugDataService : IDebugDataService
{
	private readonly HttpClient httpClient;

	public WebDebugDataService(HttpClient httpClient)
	{
		this.httpClient = httpClient;
	}

	public Task<IFileContent> GetDebugFileAsync()
		=> Task.FromResult<IFileContent>(new DebugFileContent("test-sas.txt", () => httpClient.GetStreamAsync("Data/test-sas.txt")));

	public sealed record DebugFileContent(string NameWithExtension, Func<Task<Stream>> GetStream) : IFileContent
	{
		public string? Id => null;
		public string Name => Path.GetFileNameWithoutExtension(NameWithExtension);

		public bool CanRead => true;
		public bool CanWrite => false;

		public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default) => throw new NotSupportedException();

		public Task<Stream> ReadAsync(CancellationToken cancellation = default)
			=> GetStream();
	}
}
#endif
