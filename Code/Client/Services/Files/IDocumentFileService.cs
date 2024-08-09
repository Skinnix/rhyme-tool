using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Services.Files;

public interface IDocumentFileService
{
	bool CanListFiles { get; }
	bool CanUploadFile { get; }
	bool CanSelectFile { get; }
	bool CanOpenDroppedFile { get; }
	bool CanSelectWorkingDirectory { get; }

	Task<SystemRequestResult<IFileList?>> TryGetFileListAsync(CancellationToken cancellation = default);
	Task<SystemRequestResult<IFileContent?>> TrySelectFileAsync(CancellationToken cancellation = default);

	Task<SystemRequestResult> TryWorkingDirectoryPermissionAsync(CancellationToken cancellation = default);
	Task<SystemRequestResult<string?>> TryGetWorkingDirectoryAsync(CancellationToken cancellation = default);
	Task<SystemRequestResult<string?>> TrySelectWorkingDirectoryAsync(CancellationToken cancellation = default);

	Task<SystemRequestResult<IFileContent?>> TryLoadFileAsync(string id, CancellationToken cancellation = default);

	Task<SystemRequestResult<IFileContent>> OpenSelectedFileAsync(IBrowserFile file, CancellationToken cancellation = default);
}

public class WebDefaultDocumentFileService(IJSRuntime js) : IDocumentFileService
{
	public bool CanListFiles => false;
	public bool CanUploadFile => true;
	public bool CanSelectFile => false;
	public bool CanOpenDroppedFile => true;
	public bool CanSelectWorkingDirectory => false;

	public Task<SystemRequestResult<IFileList?>> TryGetFileListAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<SystemRequestResult> TryWorkingDirectoryPermissionAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<SystemRequestResult<string?>> TryGetWorkingDirectoryAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<SystemRequestResult<string?>> TrySelectWorkingDirectoryAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<SystemRequestResult<IFileContent?>> TrySelectFileAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<SystemRequestResult<IFileContent?>> TryLoadFileAsync(string id, CancellationToken cancellation = default) => throw new NotSupportedException();

	public Task<SystemRequestResult<IFileContent>> OpenSelectedFileAsync(IBrowserFile file, CancellationToken cancellation = default)
		=> Task.FromResult(new SystemRequestResult<IFileContent>(SystemRequestResultType.Granted, new WebFileContent(js, file)));

	private sealed class WebFileContent(IJSRuntime js, IBrowserFile file) : IFileContent
	{
		private MemoryStream? content;

		string? IFileContent.Id => null;

		string IFileContent.NameWithExtension => file.Name;
		string IFileContent.Name => Path.GetFileNameWithoutExtension(file.Name);

		public bool CanRead => true;
		public bool CanWrite => true;

		public async Task<Stream> ReadAsync(CancellationToken cancellation = default)
		{
			if (content is null)
			{
				Console.WriteLine("Test");
				using var stream = file.OpenReadStream(cancellationToken: cancellation);

				content = new MemoryStream();
				await stream.CopyToAsync(content, cancellation);
			}

			content.Seek(0, SeekOrigin.Begin);
			var result = new MemoryStream(content.Length <= int.MaxValue ? (int)content.Length : int.MaxValue);
			await content.CopyToAsync(result, cancellation);
			result.Seek(0, SeekOrigin.Begin);
			return result;
		}

		public async Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
		{
			byte[] data;
			using (var stream = new MemoryStream())
			{
				await write(stream);
				data = stream.ToArray();
			}

			await js.InvokeVoidAsync("saveAsFile", data, file.Name);
		}
	}
}

public interface IFileListItemParent
{
	Task<IReadOnlyList<IFileListItem>> GetItemsAsync(CancellationToken cancellation = default);
}

public interface IFileList : IFileListItemParent
{
}

public interface IFileListItem
{
	IFileListItemParent Parent { get; }
	IFileListDirectory? ParentDirectory => Parent as IFileListDirectory;

	string Name { get; }
	DateTime? LastModified { get; }
}

public interface IFileListDirectory : IFileListItem, IFileListItemParent
{

}

public interface IFileListFile : IFileListItem
{
	long? Size { get; }

	Task<IFileContent?> GetContentAsync(CancellationToken cancellation = default);
}

public interface IFileContent
{
	string? Id { get; }
	string Name { get; }
	string NameWithExtension { get; }

	bool CanRead { get; }
	bool CanWrite { get; }

	Task<Stream> ReadAsync(CancellationToken cancellation = default);
	Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default);
}
