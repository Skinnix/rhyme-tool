using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.RhymeTool.Client.Services;

#if DEBUG
public interface IDebugDataService
{
	Task<IFileContent> GetDebugFileAsync();
}
#endif