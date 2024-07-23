using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Hosting;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Data.Notation;

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();

#if DEBUG
app.UseBlazorFrameworkFiles("/chords");
#endif

app.UseStaticFiles();

app.UseRouting();

//app.MapRazorPages();
//app.MapControllers();

#if SERVER_SIDE
app.Services.UseRhymeToolClient();


app.MapWhen(ctx => ctx.Request.Path.StartsWithSegments("/chords"), chords =>
{
	chords.UseBlazorFrameworkFiles("/chords");
	chords.UseRouting();
	chords.UseEndpoints(endpoints =>
	{
		endpoints.MapFallbackToPage("/chords/{*path:nonfile}", "/_Host");
	});
});

app.MapBlazorHub("/chords/_blazor");
app.MapFallbackToPage("/_Host");

#else

app.UseBlazorFrameworkFiles("/chords");
app.MapFallbackToFile("/chords/{*path:nonfile}", "/chords/index.html");

app.Map("/chords", app =>
{
	app.UseRouting();
	app.UseEndpoints(endpoints =>
	{
		endpoints.MapFallbackToFile("/chords/{*path:nonfile}", "/chords/index.html");
	});
});
#endif

#if SERVER_SIDE && DEBUG
var documentService = app.Services.GetRequiredService<IDocumentService>();
var debugData = app.Services.GetRequiredService<IDebugDataService>();
var documentSource = await documentService.LoadFile(await debugData.GetDebugFileAsync());
documentService.SetCurrentDocument(documentSource);
#endif

app.Run();
