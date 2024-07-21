using Microsoft.Extensions.DependencyInjection;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.RhymeTool.Client;

public static class ClientStartup
{
	public static IServiceCollection AddRhymeToolClient(this IServiceCollection services)
	{
		services.AddScoped<IDialogService, DialogService>();
		services.AddSingleton<IDocumentFileService, WebDefaultDocumentFileService>();
		services.AddSingleton<IDocumentService, DefaultDocumentService>();
		services.AddSingleton<IPreferencesService, InMemoryPreferencesService>();

		return services;
	}

	public static IServiceProvider UseRhymeToolClient(this IServiceProvider services)
	{
		return services;
	}
}
