namespace Skinnix.RhymeTool.Client.Services;

#if DEBUG
public interface IDebugDataService
{
	Task<Stream> GetDebugFileAsync();
}
#endif