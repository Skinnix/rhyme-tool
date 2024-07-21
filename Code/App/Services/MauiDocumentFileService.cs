using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

internal class MauiDocumentFileService(IPreferencesService preferences) : IDocumentFileService
{
	private const string WORKING_DIRECTORY_KEY = "WorkingDirectory";

	public bool CanListFiles => true;
	public bool CanUploadFile => true;
	public bool CanSelectFile => true;
	public bool CanOpenDroppedFile => false;
	public bool CanSelectWorkingDirectory => true;

	public Task<IFileList?> TryGetFileListAsync(CancellationToken cancellation = default)
	{
		try
		{
			var workingDirectory = preferences.GetValue<string>(WORKING_DIRECTORY_KEY);
			if (workingDirectory is not null)
			{
				var info = new DirectoryInfo(workingDirectory);
				var folders = info.GetDirectories();
				var files = info.GetFiles();
				return Task.FromResult<IFileList?>(new RootFileList(this, folders, files));
			}
		}
		catch (Exception) { }

		return Task.FromResult<IFileList?>(null);
	}

	public async Task<IFileContent?> TrySelectFileAsync(CancellationToken cancellation = default)
	{
		try
		{
			var file = await FilePicker.Default.PickAsync();
			if (file is not null)
				return new FileContent(new FileInfo(file.FullPath));
		}
		catch (Exception) { }

		return null;
	}

	public Task<IFileContent?> LoadFile(string id, CancellationToken cancellation = default)
	{
		try
		{
			//Lade Datei
			var info = new FileInfo(id);
			if (info.Exists)
				return Task.FromResult<IFileContent?>(new FileContent(info));
		}
		catch (Exception) { }

		return Task.FromResult<IFileContent?>(null);
	}

	public Task<string?> TryGetWorkingDirectory(CancellationToken cancellation = default)
	{
		try
		{
			var workingDirectory = preferences.GetValue<string>(WORKING_DIRECTORY_KEY);
			return Task.FromResult(workingDirectory);
		}
		catch (Exception) { }

		return Task.FromResult<string?>(null);
	}

	public async Task<string?> TrySelectWorkingDirectory(CancellationToken cancellation = default)
	{
		try
		{
			var result = await FolderPicker.Default.PickAsync(cancellation);
			if (result.IsSuccessful)
			{
				preferences.SetValue(WORKING_DIRECTORY_KEY, result.Folder.Path);
				return result.Folder.Path;
			}
		}
		catch (Exception) { }

		return null;
	}

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
					using var stream = Owner.File.OpenWrite();
					await write(stream);
				}
			}
		}
	}
}
