using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Hosting;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Updating;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Web.Rhyming;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseStaticWebAssets();

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

#if SERVER_SIDE
builder.Services.AddSignalR();
builder.Services.AddServerSideBlazor()
	.AddCircuitOptions(o =>
	{
		o.DetailedErrors = true;
	});

builder.Services.AddRhymeToolClient();

//Reime
builder.Services.AddSingleton<IRhymeLoadingService, ServerSideRhymeLoadingService>();

//Update-Service
builder.Services.AddScoped<IUpdateService, UpdateService>();

#if DEBUG
builder.Services.AddSingleton<HttpClient>(services => new HttpClient { BaseAddress = new Uri("https://localhost:7105/chords/") });
builder.Services.AddSingleton<IDebugDataService, WebDebugDataService>();
#endif
#endif

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseWebAssemblyDebugging();
}
else
{
	app.UseExceptionHandler("/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

//app.UseHttpsRedirection();

app.UsePathBase("/chords");

app.UseStaticFiles();

app.UseRouting();

//app.MapRazorPages();
//app.MapControllers();

#if SERVER_SIDE
app.Services.UseRhymeToolClient();


//app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/chords"), chords =>
//{
//	chords.UseBlazorFrameworkFiles("/chords");
//	chords.UseRouting();
//	chords.UseEndpoints(endpoints =>
//	{
//		endpoints.MapFallbackToPage("/chords/{*path:nonfile}", "/_Host");
//	});
//});

app.UseBlazorFrameworkFiles();
app.UseRouting();

app.MapBlazorHub("/_blazor", options =>
{
	options.WebSockets.CloseTimeout = TimeSpan.FromSeconds(5);
});
app.MapFallbackToPage("/_Host");

#else

app.UseBlazorFrameworkFiles();
app.UseRouting();
app.MapFallbackToFile("/{*path:nonfile}", "/index.html");
#endif

#if SERVER_SIDE && DEBUG
var documentService = app.Services.GetRequiredService<IDocumentService>();
var debugData = app.Services.GetRequiredService<IDebugDataService>();
var documentSource = await documentService.LoadFile(await debugData.GetDebugFileAsync());
documentService.SetCurrentDocument(documentSource);
#endif

app.Run();
