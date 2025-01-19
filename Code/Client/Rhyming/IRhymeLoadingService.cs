using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Skinnix.RhymeTool.Client.Rhyming;

public interface IRhymeLoadingService
{
	RhymeHelper? LoadedRhymeHelper { get; }

	Task<RhymeHelper> LoadRhymeHelperAsync();
}
