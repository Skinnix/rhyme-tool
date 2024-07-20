using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

internal class MauiDocumentFileService : IDocumentFileService
{
	private string? workingDirectory = null;

	public bool CanListFiles => true;
	public bool CanUploadFile => true;
	public bool CanSelectFile => true;
	public bool CanOpenDroppedFile => false;
	public bool CanSelectWorkingDirectory => true;

	public async Task<IFileList?> TryGetFileListAsync(CancellationToken cancellation = default)
	{
		if (workingDirectory is null)
		{
			if (!await TrySelectWorkingDirectory(cancellation) || workingDirectory is null)
				return null;
		}

		try
		{
			var info = new DirectoryInfo(workingDirectory);
			var folders = info.GetDirectories();
			var files = info.GetFiles(".txt");
			return new RootFileList(this, folders, files);
		}
		catch (Exception)
		{
			return null;
		}
	}

	public Task<IFileContent?> TrySelectFileAsync(CancellationToken cancellation = default) => throw new NotImplementedException();

	public async Task<bool> TrySelectWorkingDirectory(CancellationToken cancellation = default)
	{
		var result = await FolderPicker.Default.PickAsync(cancellation);
		if (!result.IsSuccessful)
			return false;

		workingDirectory = result.Folder.Path;
		return true;
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
				.Concat(Directory.EnumerateFiles("*.txt")
					.Select(file => new FileItem(this, file, file.Name, file.LastAccessTime, file.Length)))
				.ToArray());
		}

		private record FileItem(IFileListItemParent Parent, FileInfo File, string Name, DateTime? LastModified, long? Size) : IFileListFile
		{
			public Task<IFileContent?> GetContentAsync(CancellationToken cancellation = default)
				=> Task.FromResult<IFileContent?>(new FileContent(this));

			private record FileContent(FileItem Owner) : IFileContent
			{
				public string Name => Owner.Name;

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
