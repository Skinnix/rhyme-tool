using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.IO;

namespace Skinnix.RhymeTool.Client.Updating;

public record AppVersionInfoData
{
	public Dictionary<string, PlatformInfo> Platforms { get; }

	private AppVersionInfoData(Dictionary<string, PlatformInfo> platforms)
	{
		Platforms = platforms;
	}

	public static bool TryRead(IniFile ini, string baseUrl, [MaybeNullWhen(false)] out AppVersionInfoData data)
	{
		var platforms = new Dictionary<string, PlatformInfo>();
		foreach (var section in ini.Sections)
		{
			if (!section.Key.StartsWith("Platform:", ini.DefaultComparison))
				continue;
			var platformKey = section.Key["Platform:".Length..];

			if (!section.Value.TryGetValue("Label", out var label)
				|| !section.Value.TryGetValue("URL", out var url)
				|| !section.Value.TryGetValue("Version", out var versionString)
				|| !Version.TryParse(versionString, out var version))
				continue;

			platforms[platformKey.ToLower()] = new PlatformInfo(label, version, baseUrl + url);
		}

		if (platforms.Count == 0)
		{
			data = null;
			return false;
		}

		data = new AppVersionInfoData(platforms);
		return true;
	}

	public record PlatformInfo(string Label, Version Version, string Url);
}
