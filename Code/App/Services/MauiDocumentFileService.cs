using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Microsoft.AspNetCore.Components.Forms;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Client.Services.Preferences;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

internal class MauiDocumentFileService(IPreferencesService preferenceService) : IDocumentFileService
{
	private class PreferencesHolder(IPreferencesService preferenceService) : PreferenceObject(preferenceService, "Files_")
	{
		public string? WorkingDirectory
		{
			get => GetValue<string>();
			set => SetValue(value);
		}
	}

	private PreferencesHolder preferences = new(preferenceService);

	public bool CanListFiles => true;
	public bool CanUploadFile => true;
	public bool CanSelectFile => true;
	public bool CanOpenDroppedFile => false;
	public bool CanSelectWorkingDirectory => true;

	public Task<SystemRequestResult<IFileList?>> TryGetFileListAsync(CancellationToken cancellation = default)
		=> Task.FromResult(TryGetFileList(cancellation));
	public SystemRequestResult<IFileList?> TryGetFileList(CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		var getPermissionResult = TryGetExternalStoragePermission(request: false);
		if (!getPermissionResult.HasFlag(SystemRequestResultType.OkFlag))
			return getPermissionResult;

		if (preferences.WorkingDirectory is null)
			return new(SystemRequestResultType.Existing, null);
		
		try
		{
			var info = new DirectoryInfo(preferences.WorkingDirectory);
			var folders = info.GetDirectories();
			var files = info.GetFiles();
			return new(SystemRequestResultType.Existing, new RootFileList(this, folders, files));
		}
		catch (Exception)
		{
			return SystemRequestResultType.Error;
		}
	}

	public async Task<SystemRequestResult<IFileContent?>> TrySelectFileAsync(CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		try
		{
			var file = await FilePicker.Default.PickAsync();
			cancellation.ThrowIfCancellationRequested();
			if (file is not null)
				return new(SystemRequestResultType.Confirmed, new FileContent(new FileInfo(file.FullPath)));
			else
				return new(SystemRequestResultType.Confirmed, null);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			return SystemRequestResultType.Error;
		}
	}

	public Task<SystemRequestResult<IFileContent?>> TryLoadFileAsync(string id, CancellationToken cancellation = default)
		=> Task.FromResult(LoadFile(id, cancellation));
	public SystemRequestResult<IFileContent?> LoadFile(string id, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		try
		{
			//Lade Datei
			var info = new FileInfo(id);
			if (info.Exists)
				return new(SystemRequestResultType.Existing, new FileContent(info));
			else
				return new(SystemRequestResultType.Existing, null);
		}
		catch (Exception)
		{
			return SystemRequestResultType.Error;
		}
	}

	public Task<SystemRequestResult> TryWorkingDirectoryPermissionAsync(CancellationToken cancellation = default)
		=> Task.FromResult<SystemRequestResult>(TryGetExternalStoragePermission(request: true, cancellation));

	private SystemRequestResultType TryGetExternalStoragePermission(bool request, CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();

#if ANDROID30_0_OR_GREATER
		try
		{
			if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.R)
				return SystemRequestResultType.NotNeeded;

			if (Platform.CurrentActivity is null)
				return SystemRequestResultType.PrerequisiteFailed;

			if (Android.OS.Environment.IsExternalStorageManager)
				return SystemRequestResultType.Existing;

			if (!request)
				return SystemRequestResultType.Nonexisting;

			var manage = Android.Provider.Settings.ActionManageAppAllFilesAccessPermission;
			var intent = new Android.Content.Intent(manage);
			var uri = Android.Net.Uri.Parse("package:" + AppInfo.Current.PackageName);
			intent.SetData(uri);
			Platform.CurrentActivity.StartActivity(intent);
			return SystemRequestResultType.Requesting;
		}
		catch (Exception)
		{
			return SystemRequestResultType.PrerequisiteFailed;
		}
# elif IOS
		return SystemRequestResultType.PrerequisiteFailed;
#else
		return SystemRequestResultType.NotNeeded;
#endif
	}

	public Task<SystemRequestResult<string?>> TryGetWorkingDirectoryAsync(CancellationToken cancellation = default)
		=> Task.FromResult(TryGetWorkingDirectory(cancellation));
	public SystemRequestResult<string?> TryGetWorkingDirectory(CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();

		try
		{
			var permission = TryGetExternalStoragePermission(request: false, cancellation);
			if (!permission.HasFlag(SystemRequestResultType.OkFlag))
				return permission;

			return new(SystemRequestResultType.Existing, preferences.WorkingDirectory);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			return SystemRequestResultType.Error;
		}
	}

	public async Task<SystemRequestResult<string?>> TrySelectWorkingDirectoryAsync(CancellationToken cancellation = default)
	{
		cancellation.ThrowIfCancellationRequested();
		try
		{
			var permission = TryGetExternalStoragePermission(request: true, cancellation);
			if (!permission.HasFlag(SystemRequestResultType.OkFlag))
				return permission;

			var result = await FolderPicker.Default.PickAsync(cancellation);
			if (!result.IsSuccessful)
				return SystemRequestResultType.Denied;

			cancellation.ThrowIfCancellationRequested();
			preferences.WorkingDirectory = result.Folder.Path;
			return new(SystemRequestResultType.Granted, result.Folder.Path);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			return SystemRequestResultType.Error;
		}
	}

	public Task<SystemRequestResult<IFileContent>> OpenSelectedFileAsync(IBrowserFile file, CancellationToken cancellation = default)
		=> throw new NotSupportedException();

	private record FileContent(FileInfo File) : IFileContent
	{
		public string? Id => File.FullName;
		public string NameWithExtension => File.Name;
		public string Name => Path.GetFileNameWithoutExtension(File.Name);

		public bool CanRead => true;
		public bool CanWrite => true;

		public Task<Stream> ReadAsync(CancellationToken cancellation = default)
			=> Task.FromResult<Stream>(File.OpenRead());

		public async Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
		{
			using var stream = File.OpenWrite();
			await write(stream);
		}
	}

	private class RootFileList : IFileList
	{
		private readonly MauiDocumentFileService owner;
		private readonly DirectoryInfo[] folders;
		private readonly FileInfo[] files;

		public RootFileList(MauiDocumentFileService owner, DirectoryInfo[] folders, FileInfo[] files)
		{
			this.owner = owner;
			this.folders = folders;
			this.files = files;
		}

		public Task<IReadOnlyList<IFileListItem>> GetItemsAsync(CancellationToken cancellation = default)
			=> Task.FromResult<IReadOnlyList<IFileListItem>>(Enumerable.Empty<IFileListItem>()
			.Concat(folders
				.Select(folder => new DirectoryItem(this, folder, folder.Name, folder.LastAccessTime)))
			.Concat(files
				.Select(file => new FileItem(this, file, file.Name, file.LastAccessTime, file.Length)))
			.ToArray());

		private record DirectoryItem(IFileListItemParent Parent, DirectoryInfo Directory, string Name, DateTime? LastModified) : IFileListDirectory
		{
			public Task<IReadOnlyList<IFileListItem>> GetItemsAsync(CancellationToken cancellation = default)
				=> Task.FromResult<IReadOnlyList<IFileListItem>>(Enumerable.Empty<IFileListItem>()
				.Concat(Directory.EnumerateDirectories()
					.Select(folder => new DirectoryItem(this, folder, folder.Name, folder.LastAccessTime)))
				.Concat(Directory.EnumerateFiles()
					.Select(file => new FileItem(this, file, file.Name, file.LastAccessTime, file.Length)))
				.ToArray());
		}

		private record FileItem(IFileListItemParent Parent, FileInfo File, string Name, DateTime? LastModified, long? Size) : IFileListFile
		{
			public Task<IFileContent?> GetContentAsync(CancellationToken cancellation = default)
				=> Task.FromResult<IFileContent?>(new FileContent(this));

			private record FileContent(FileItem Owner) : IFileContent
			{
				public string? Id => Owner.File.FullName;
				public string Name => Path.GetFileNameWithoutExtension(Owner.File.Name);
				public string NameWithExtension => Owner.File.Name;

				public bool CanRead => true;
				public bool CanWrite => true;

				public Task<Stream> ReadAsync(CancellationToken cancellation = default)
					=> Task.FromResult<Stream>(Owner.File.OpenRead());

				public async Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
				{
					using (var stream = Owner.File.OpenWrite())
					{
						await write(stream);
					}
				}
			}
		}
	}
}
