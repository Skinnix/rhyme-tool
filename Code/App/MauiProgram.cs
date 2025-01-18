using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Rhyming;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Client.Services.Preferences;
using Skinnix.RhymeTool.Client.Updating;
using Skinnix.RhymeTool.MauiBlazor.Rhyming;
using Skinnix.RhymeTool.MauiBlazor.Services;
using Skinnix.RhymeTool.MauiBlazor.Updating;

namespace Skinnix.RhymeTool.MauiBlazor;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		//MAUI-Services
		builder.Services.AddSingleton<IMauiUiService, MauiUiService>();

		//HTTP-Client
		builder.Services.AddScoped(_ => new HttpClient());

		//Reime
		builder.Services.AddSingleton<IRhymeLoadingService, MauiRhymeLoadingService>();

		//Update-Service
		builder.Services.AddScoped<IUpdateService, MauiUpdateService>();
		builder.Services.Configure<UpdateOptions>(options =>
		{
#if WINDOWS
			options.PlatformKey = "windows";
#elif ANDROID
			options.PlatformKey = "android";
#endif
		});

		//RhymeTool Client Services
		builder.Services.AddRhymeToolClient();

		//Service-Überschreibungen
		builder.Services.AddSingleton<IDocumentFileService, MauiDocumentFileService>();
		builder.Services.AddTransient<IPreferencesService, MauiPreferencesService>();
		builder.Services.AddSingleton<IDialogService, MauiDialogService>();

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
		public Task<IFileContent> GetDebugFileAsync()
			=> Task.FromResult<IFileContent>(new DebugFileContent("test-sas.txt", () => FileSystem.OpenAppPackageFileAsync("test-sas.txt")));

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
}
