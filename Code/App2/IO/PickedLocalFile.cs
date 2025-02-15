using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.Compoetry.Maui.IO;

public record PickedLocalFile(FileResult File) : IFileContent
{
	public string? Id => File.FullPath;
	public string Name => Path.GetFileNameWithoutExtension(File.FileName);
	public string NameWithExtension => File.FileName;

	public bool CanRead => true;
	public bool CanWrite => false;

	public Task<Stream> ReadAsync(CancellationToken cancellation = default) => File.OpenReadAsync();

	public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
		=> throw new NotSupportedException("Datei kann nicht geschrieben werden.");
}
