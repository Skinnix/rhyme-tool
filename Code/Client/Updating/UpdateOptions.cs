using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.Updating;

public class UpdateOptions
{
	public string UpdateBaseUrl { get; set; } = "https://skinnix.net/chords/update/";
	public string UpdateVersionUrlSuffix { get; set; } = "check?current={0}";
	
	public string PlatformKey { get; set; } = "unknown";
}
