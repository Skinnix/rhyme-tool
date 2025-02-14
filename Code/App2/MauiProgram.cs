using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Skinnix.Compoetry.Maui.Views;

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

		builder.Services.AddSingleton<IMauiUiService, MauiUiService>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		#region ViewModels
		builder.Services.AddTransient<MainPageVM>();
		#endregion

		return builder.Build();
	}
}
