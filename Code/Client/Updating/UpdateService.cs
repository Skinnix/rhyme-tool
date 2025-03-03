﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Options;
using System.Reflection;
using Skinnix.RhymeTool.IO;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Updating;

public interface IUpdateService
{
	Version? CurrentVersion { get; }
	string? CurrentVersionString { get; }

	bool IsUpdateAvailable { get; }
	bool IsDownloadAvailable { get; }

	Task<ICheckUpdateResult> CheckUpdatesAsync();
	Task<ICheckDownloadsResult> CheckDownloadsAsync();

	public interface IDownloadElement
	{
		Version Version { get; }
		string Label { get; }

		string? DownloadUrl { get; }

		Task StartDownload();
	}

	public interface ICheckUpdateResult
	{
		string? ErrorMessage { get; }
		IDownloadOption? Download { get; }

		[MemberNotNullWhen(false, nameof(ErrorMessage))]
		bool CheckSuccess => ErrorMessage is null;

		Version? CurrentVersion { get; }

		[MemberNotNullWhen(true, nameof(Download))]
		bool IsUpdateAvailable => Download is not null && (CurrentVersion is null || Download.Version > CurrentVersion);

		public interface IDownloadOption : IDownloadElement;
	}

	public interface ICheckDownloadsResult
	{
		string? ErrorMessage { get; }
		bool CheckSuccess => ErrorMessage is null;

		IReadOnlyCollection<IDownloadOption> DownloadOptions { get; }

		public interface IDownloadOption : IDownloadElement;
	}
}

public abstract class UpdateServiceBase(IOptions<UpdateOptions> options, HttpClient httpClient, bool isUpdateAvailable, bool isDownloadAvailable) : IUpdateService
{
	public Version? CurrentVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version;
	public string? CurrentVersionString => CurrentVersion is null ? null : "Version " + CurrentVersion?.ToString(2);

	public bool IsUpdateAvailable { get; } = isUpdateAvailable;
	public bool IsDownloadAvailable { get; } = isDownloadAvailable;

	public virtual async Task<IUpdateService.ICheckUpdateResult> CheckUpdatesAsync()
	{
		try
		{
			AppVersionInfoData updateData;
			var checkUrl = options.Value.UpdateBaseUrl + string.Format(options.Value.UpdateVersionUrlSuffix, CurrentVersion);
			using (var infoStream = await httpClient.GetStreamAsync(checkUrl))
			{
				using var reader = new StreamReader(infoStream);
				var data = await IniFile.ReadAsync(reader, StringComparison.OrdinalIgnoreCase);
				if (!AppVersionInfoData.TryRead(data, options.Value.UpdateBaseUrl, out updateData!))
					return new CheckUpdateResult.Error("Leere oder ungültige Antwort", CurrentVersion);
			}

			if (!updateData.Platforms.TryGetValue(options.Value.PlatformKey, out var platformInfo))
				return new CheckUpdateResult.Error("Plattform nicht gefunden", CurrentVersion);

			if (CurrentVersion is not null && CurrentVersion >= platformInfo.Version)
				return new CheckUpdateResult.NoUpdate(CurrentVersion);

			return new CheckUpdateResult.UpdateAvailable(this, CurrentVersion, platformInfo.Version, platformInfo.Label, platformInfo.Url);
		}
		catch (HttpRequestException)
		{
			return new CheckUpdateResult.Error("Keine Serververbindung", CurrentVersion);
		}
		catch (TaskCanceledException)
		{
			return new CheckUpdateResult.Error("Serververbindung abgebrochen", CurrentVersion);
		}
		catch (Exception ex)
		{
			return new CheckUpdateResult.Error("Unbekannter Fehler: " + ex.Message, CurrentVersion);
		}
	}

	public virtual async Task<IUpdateService.ICheckDownloadsResult> CheckDownloadsAsync()
	{
		try
		{
			AppVersionInfoData updateData;
			var checkUrl = options.Value.UpdateBaseUrl + string.Format(options.Value.UpdateVersionUrlSuffix, CurrentVersion);
			using (var infoStream = await httpClient.GetStreamAsync(checkUrl))
			{
				using var reader = new StreamReader(infoStream);
				var data = await IniFile.ReadAsync(reader, StringComparison.OrdinalIgnoreCase);
				if (!AppVersionInfoData.TryRead(data, options.Value.UpdateBaseUrl, out updateData!))
					return new CheckDownloadsResult.Error("Leere oder ungültige Antwort");
			}

			return new CheckDownloadsResult.DownloadAvailable(this, updateData.Platforms);
		}
		catch (HttpRequestException)
		{
			return new CheckDownloadsResult.Error("Keine Serververbindung");
		}
		catch (TaskCanceledException)
		{
			return new CheckDownloadsResult.Error("Serververbindung abgebrochen");
		}
		catch (Exception ex)
		{
			return new CheckDownloadsResult.Error("Unbekannter Fehler: " + ex.Message);
		}
	}

	protected abstract Task StartDownload(IUpdateService.IDownloadElement download, string url);

	private abstract record DownloadElement(UpdateServiceBase Owner, Version Version, string Label, string DownloadUrl) : IUpdateService.IDownloadElement
	{
		public virtual Task StartDownload()
			=> Owner.StartDownload(this, DownloadUrl);
	}

	private abstract record CheckUpdateResult(string? ErrorMessage, Version? CurrentVersion,
		IUpdateService.ICheckUpdateResult.IDownloadOption? Download) :
		IUpdateService.ICheckUpdateResult
	{
		public sealed record Error(string ErrorMessage, Version? CurrentVersion) : CheckUpdateResult(ErrorMessage, CurrentVersion, null);
		public sealed record NoUpdate(Version? CurrentVersion) : CheckUpdateResult(null, CurrentVersion, null);

		public sealed record UpdateAvailable(UpdateServiceBase Owner, Version? CurrentVersion, Version UpdateVersion, string Label, string DownloadUrl) :
			CheckUpdateResult(null, CurrentVersion, new DownloadOption(Owner, UpdateVersion, Label, DownloadUrl))
		{
			public sealed record DownloadOption(UpdateServiceBase Owner, Version Version, string Label, string DownloadUrl) : DownloadElement(Owner, Version, Label, DownloadUrl),
				IUpdateService.ICheckUpdateResult.IDownloadOption;
		}
	}

	private abstract record CheckDownloadsResult(string? ErrorMessage, IReadOnlyCollection<IUpdateService.ICheckDownloadsResult.IDownloadOption> DownloadOptions) :
		IUpdateService.ICheckDownloadsResult
	{
		public sealed record Error(string ErrorMessage) : CheckDownloadsResult(ErrorMessage, []);

		public sealed record DownloadAvailable(UpdateServiceBase Owner, IEnumerable<KeyValuePair<string, AppVersionInfoData.PlatformInfo>> Platforms) :
			CheckDownloadsResult(null, [..Platforms.Select(u => new DownloadOption(Owner, u.Value.Version, u.Value.Label, u.Value.Url))])
		{

			public sealed record DownloadOption(UpdateServiceBase Owner, Version Version, string Label, string DownloadUrl) : DownloadElement(Owner, Version, Label, DownloadUrl),
			IUpdateService.ICheckDownloadsResult.IDownloadOption;
		}
	}
}

public class UpdateService(IOptions<UpdateOptions> options, HttpClient httpClient, IJSRuntime js) : UpdateServiceBase(options, httpClient, false, true)
{
	protected override Task StartDownload(IUpdateService.IDownloadElement element, string url)
		=> js.InvokeVoidAsync("downloadFile", url, element.Label + Path.GetExtension(url)).AsTask();
}
