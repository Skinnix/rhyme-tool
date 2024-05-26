using Microsoft.AspNetCore.ResponseCompression;
using Skinnix.RhymeTool.Client;
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

builder.Services.AddRhymeToolClient("http://localhost:5276/");
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
app.UseBlazorFrameworkFiles(); // "/chords");
#endif

app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();

#if SERVER_SIDE
app.Services.UseRhymeToolClient();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
#else
app.MapFallbackToFile("/index.html");
#endif

app.Run();
