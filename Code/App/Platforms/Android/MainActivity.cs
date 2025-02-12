using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.MauiBlazor.Intents;

namespace Skinnix.RhymeTool.MauiBlazor;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density, Exported = true, LaunchMode = LaunchMode.SingleTop)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataSchemes = ["file", "content"], DataHost = "*", DataMimeType = "*/*", DataPathPattern = ".*\\.cps")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataSchemes = ["file", "content"], DataMimeType = "text/plain")]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnNewIntent(Intent? intent)
	{
		if (intent?.Action is Intent.ActionView or Intent.ActionEdit)
		{
			if (intent.Data is not null)
			{
				var data = intent.Data;
				var fileNameWithExtension = data.LastPathSegment;
				var fileName = Path.GetFileNameWithoutExtension(fileNameWithExtension);
				var readStream = () => ContentResolver?.OpenInputStream(data);
				var writeStream = () => ContentResolver?.OpenOutputStream(data);

				var file = new IntentFile(null, fileName ?? string.Empty, fileNameWithExtension ?? string.Empty, readStream, writeStream);
				MauiProgram.LaunchIntent = new OpenFileIntent(file);
			}
		}

		base.OnNewIntent(intent);
	}

	private record IntentFile(string? Id, string Name, string NameWithExtension, Func<Stream?> ReadStream, Func<Stream?> WriteStream) : IFileContent
	{
		public bool CanRead { get; init; } = true;
		public bool CanWrite { get; init; } = true;

		public Task<Stream> ReadAsync(CancellationToken cancellation = default)
			=> Task.FromResult(ReadStream() ?? throw new InvalidOperationException("Fehler beim Lesen der Datei"));

		public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default)
			=> write(WriteStream() ?? throw new InvalidOperationException("Fehler beim Schreiben der Datei"));
	}
}
