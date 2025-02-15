using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Skinnix.Compoetry.Maui.Views;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.Compoetry.Maui.Pages;
using Skinnix.Compoetry.Maui.Pages.Document;

namespace Skinnix.Compoetry.Maui;

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
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddMauiBlazorWebView();

		//RhymeTool
		builder.Services.AddRhymeToolClient();

		//HTTP-Client
		builder.Services.AddScoped(_ => new HttpClient());

		builder.Services.AddSingleton<IMauiUiService, MauiUiService>();
		builder.Services.AddTransient<IDebugDataService, MauiDebugDataService>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		#region ViewModels
		builder.Services.AddTransient<MainPageVM>();

		builder.Services.AddWithRoute<RendererPage, RendererPageVM>();
		#endregion

		return builder.Build();
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

public static class MauiExtensions
{
	public static IServiceCollection AddWithRoute<TPage, TViewModel>(this IServiceCollection services)
		where TPage : NavigableElement, IHasShellRoute
		where TViewModel : ViewModelBase
		=> services.AddTransientWithShellRoute<TPage, TViewModel>(TPage.Route);
}
