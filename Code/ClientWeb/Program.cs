using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Skinnix.RhymeTool;
using Skinnix.RhymeTool.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

Console.WriteLine(builder.HostEnvironment.BaseAddress);

builder.Services.AddRhymeToolClient(builder.HostEnvironment.BaseAddress);

var host = builder.Build();

host.Services.UseRhymeToolClient();

await host.RunAsync();