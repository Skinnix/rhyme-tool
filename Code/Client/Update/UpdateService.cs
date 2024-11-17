using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Skinnix.RhymeTool.Updating;

public interface IUpdateService
{
	Task<ICheckUpdateResult> CheckUpdatesAsync();

	public interface ICheckUpdateResult
	{
		string? ErrorMessage { get; }
		bool CheckSuccess => ErrorMessage is null;

		Version? CurrentVersion { get; }
		Version? UpdateVersion { get; }

		bool IsUpdateAvailable => UpdateVersion is not null && (CurrentVersion is null || UpdateVersion > CurrentVersion);

		Task StartDownload();
	}
}

public abstract class UpdateServiceBase : IUpdateService
{
	private readonly IOptions<UpdateOptions> options;
	private readonly HttpClient httpClient;
	private readonly Version? currentVersion;

	public UpdateServiceBase(IOptions<UpdateOptions> options, HttpClient httpClient)
	{
		this.options = options;
		this.httpClient = httpClient;

		this.currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
	}

	public virtual async Task<IUpdateService.ICheckUpdateResult> CheckUpdatesAsync()
	{
		try
		{
			var checkUrl = string.Format(options.Value.UpdateVersionUrl, currentVersion);
			var versionInfo = options.Value.UpdateBaseUrl + await httpClient.GetStringAsync(checkUrl);

			var lines = versionInfo.Split('\n');
			if (!Version.TryParse(lines[^1].Trim(), out var updateVersion))
				return new CheckUpdateResult.Error("Fehler beim Lesen der Versionsnummer", currentVersion);

			if (currentVersion is not null && currentVersion >= updateVersion)
				return new CheckUpdateResult.NoUpdate(currentVersion, updateVersion);

			var platformEquals = options.Value.PlatformName + "=";
			var platformFileName = lines.SkipLast(1)
				.FirstOrDefault(l => l.StartsWith(platformEquals, StringComparison.OrdinalIgnoreCase))?[platformEquals.Length..];

			if (platformFileName is null)
				return new CheckUpdateResult.Error("Plattform nicht gefunden", currentVersion);

			var downloadUrl = options.Value.UpdateBaseUrl + string.Format(options.Value.UpdateDownloadUrl, updateVersion, platformFileName);
			return new CheckUpdateResult.UpdateAvailable(this, currentVersion, updateVersion, downloadUrl);
		}
		catch (HttpRequestException)
		{
			return new CheckUpdateResult.Error("Keine Serververbindung", currentVersion);
		}
		catch (TaskCanceledException)
		{
			return new CheckUpdateResult.Error("Serververbindung abgebrochen", currentVersion);
		}
		catch (Exception ex)
		{
			return new CheckUpdateResult.Error("Unbekannter Fehler: " + ex.Message, currentVersion);
		}
	}

	protected abstract Task StartDownload(string url);

	private abstract record CheckUpdateResult(string? ErrorMessage, Version? CurrentVersion, Version? UpdateVersion) : IUpdateService.ICheckUpdateResult
	{
		public abstract Task StartDownload();

		public sealed record Error(string ErrorMessage, Version? CurrentVersion) : CheckUpdateResult(ErrorMessage, CurrentVersion, null)
		{
			public override Task StartDownload() => throw new NotSupportedException();
		}

		public sealed record NoUpdate(Version? CurrentVersion, Version UpdateVersion) : CheckUpdateResult(null, CurrentVersion, UpdateVersion)
		{
			public override Task StartDownload() => throw new NotSupportedException();
		}

		public sealed record UpdateAvailable(UpdateServiceBase Owner, Version? CurrentVersion, Version UpdateVersion, string DownloadUrl) :
			CheckUpdateResult(null, CurrentVersion, UpdateVersion)
		{
			public override Task StartDownload()
				=> Owner.StartDownload(DownloadUrl);
		}
	}
}
