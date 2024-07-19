using Microsoft.Extensions.Logging;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Services;

namespace Skinnix.RhymeTool.MauiBlazor;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddRhymeToolClient("");

#if DEBUG
		builder.Services.AddScoped<IDebugDataService, MauiDebugDataService>();
#endif

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		app.Services.UseRhymeToolClient();

		return app;
	}

#if DEBUG
	private class MauiDebugDataService : IDebugDataService
	{
		public Task<Stream> GetDebugFileAsync()
		{
			return FileSystem.OpenAppPackageFileAsync("test-sas.txt");
		}
	}
#endif
}
