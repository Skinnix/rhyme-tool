using Skinnix.RhymeTool.Client.Services.Files;

namespace Skinnix.RhymeTool.MauiBlazor.Intents;

public sealed record OpenFileIntent(IFileContent File) : LaunchIntent(false);
