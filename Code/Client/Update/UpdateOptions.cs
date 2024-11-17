using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Updating;

public class UpdateOptions
{
	public string UpdateBaseUrl { get; set; } = "https://skinnix.net/chords/update/";
	public string UpdateVersionUrl { get; set; } = "newest?version={0}";
	public string UpdateDownloadUrl { get; set; } = "version/{0}/{1}";
	
	public string PlatformName { get; set; } = "unknown";
}
