using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.Services.Files;

public interface IDocumentFileService
{
	bool CanListFiles { get; }
	bool CanUploadFile { get; }
	bool CanSelectFile { get; }
	bool CanOpenDroppedFile { get; }
	bool CanSelectWorkingDirectory { get; }

	Task<IFileList?> TryGetFileListAsync(CancellationToken cancellation = default);
	Task<IFileContent?> TrySelectFileAsync(CancellationToken cancellation = default);

	Task<string?> TryGetWorkingDirectory(CancellationToken cancellation = default);
	Task<string?> TrySelectWorkingDirectory(CancellationToken cancellation = default);

	Task<IFileContent?> LoadFile(string id, CancellationToken cancellation = default);
}

public class WebDefaultDocumentFileService : IDocumentFileService
{
	public bool CanListFiles => false;
	public bool CanUploadFile => true;
	public bool CanSelectFile => false;
	public bool CanOpenDroppedFile => true;
	public bool CanSelectWorkingDirectory => false;

	public Task<IFileList?> TryGetFileListAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<string?> TryGetWorkingDirectory(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<string?> TrySelectWorkingDirectory(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<IFileContent?> TrySelectFileAsync(CancellationToken cancellation = default) => throw new NotSupportedException();
	public Task<IFileContent?> LoadFile(string id, CancellationToken cancellation = default) => throw new NotSupportedException();
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
