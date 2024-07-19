using Microsoft.Extensions.DependencyInjection;
using Skinnix.RhymeTool.Client.Services;

namespace Skinnix.RhymeTool.Client;

public static class ClientStartup
{
	public static IServiceCollection AddRhymeToolClient(this IServiceCollection services, string baseAddress)
	{
		services.AddScoped(sp => new HttpClient()
		{
			BaseAddress = new Uri(baseAddress),
		});

#if DEBUG
		services.AddScoped<IDebugDataService, DebugDataService>();
#endif

		return services;
	}

	public static IServiceProvider UseRhymeToolClient(this IServiceProvider services)
	{
		return services;
	}
}
