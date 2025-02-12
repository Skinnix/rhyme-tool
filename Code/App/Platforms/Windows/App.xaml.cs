using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.MauiBlazor.Intents;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Skinnix.RhymeTool.MauiBlazor.WinUI;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		base.OnLaunched(args);

		var launchArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
		File.WriteAllText(@"C:\Users\Hendrik\Desktop\test.txt", launchArgs.Kind.ToString());
		if (launchArgs.Data is ILaunchActivatedEventArgs activatedArgs
			&& !string.IsNullOrEmpty(activatedArgs.Arguments))
		{
			var argument = activatedArgs.Arguments;
			if (argument.StartsWith('\"'))
			{
				var index = argument.IndexOf('\"', 1);
				if (index > 0)
					argument = argument[1..index];
			}

			var file = new IntentPathFile(argument);
			MauiProgram.LaunchIntent = new OpenFileIntent(file);
		}
		else if (launchArgs.Kind == ExtendedActivationKind.File && launchArgs.Data is IFileActivatedEventArgs fileArgs
			&& fileArgs.Files.FirstOrDefault() is IStorageItem storageFile)
		{
			var file = new IntentStorageFile(storageFile);
			MauiProgram.LaunchIntent = new OpenFileIntent(file);
		}
	}

	private record IntentStorageFile(IStorageItem Item) : IFileContent
	{
		public string Id => Item.Path;
		public string Name => Path.GetFileNameWithoutExtension(NameWithExtension);
		public string NameWithExtension => Item.Name;

		public bool CanRead { get; init; } = true;
		public bool CanWrite { get; init; } = (Item.Attributes & Windows.Storage.FileAttributes.ReadOnly) == 0;

		public Task<Stream> ReadAsync(CancellationToken cancellation = default)
			=> Task.FromResult<Stream>(File.OpenRead(Item.Path));

		public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
			=> write(File.OpenWrite(Item.Path));
	}

	private record IntentPathFile(string Path) : IFileContent
	{
		private System.IO.FileAttributes? attributes = null;

		public string Id => Path;
		public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
		public string NameWithExtension => System.IO.Path.GetFileName(Path);

		public bool CanRead { get; init; } = true;
		public bool CanWrite => ((attributes ??= File.GetAttributes(Path)) & System.IO.FileAttributes.ReadOnly) == 0;

		public Task<Stream> ReadAsync(CancellationToken cancellation = default)
			=> Task.FromResult<Stream>(File.OpenRead(Path));

		public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
			=> write(File.OpenWrite(Path));
	}
}

