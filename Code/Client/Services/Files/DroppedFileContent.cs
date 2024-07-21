using Microsoft.AspNetCore.Components.Forms;

namespace Skinnix.RhymeTool.Client.Services.Files;

public sealed record DroppedFileContent(IBrowserFile File) : IFileContent
{
	string? IFileContent.Id => null;

	string IFileContent.NameWithExtension => File.Name;
	string IFileContent.Name => Path.GetFileNameWithoutExtension(File.Name);

	public bool CanRead => true;
	public bool CanWrite => false;

	public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default) => throw new NotSupportedException();

	public Task<Stream> ReadAsync(CancellationToken cancellation = default)
		=> Task.FromResult(File.OpenReadStream(cancellationToken: cancellation));
}