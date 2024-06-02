namespace Skinnix.RhymeTool.Client;

public static class ClientStartup
{
	public static IServiceCollection AddRhymeToolClient(this IServiceCollection services, string baseAddress)
	{
		services.AddScoped(sp => new HttpClient()
		{
			BaseAddress = new Uri(baseAddress),
		});

		return services;
	}

	public static IServiceProvider UseRhymeToolClient(this IServiceProvider services)
	{
		return services;
	}
}
