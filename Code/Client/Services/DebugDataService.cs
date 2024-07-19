namespace Skinnix.RhymeTool.Client.Services;

#if DEBUG
public interface IDebugDataService
{
	Task<Stream> GetDebugFileAsync();
}

public class DebugDataService : IDebugDataService
{
	private readonly HttpClient httpClient;

	public DebugDataService(HttpClient httpClient)
	{
		this.httpClient = httpClient;
	}

	public Task<Stream> GetDebugFileAsync()
		=> httpClient.GetStreamAsync("Data/test-sas.txt");
}
#endif